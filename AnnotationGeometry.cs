namespace WindowForm_Move;

public static class AnnotationGeometry
{
    private const int MinimumMemoWidth = 90;
    private const int MinimumMemoHeight = 38;
    private const int MaximumMemoWidth = 600;

    public static Font CreateMemoFont(string fontName, float fontSize)
    {
        try
        {
            return new Font(fontName, fontSize, FontStyle.Regular, GraphicsUnit.Point);
        }
        catch
        {
            return new Font("Segoe UI Semibold", fontSize, FontStyle.Regular, GraphicsUnit.Point);
        }
    }

    public static Rectangle GetMemoBounds(AnnotationArrow arrow, Rectangle virtualBounds)
    {
        var maximumTextSize = GetMaximumTextSize(virtualBounds);
        var measured = string.IsNullOrWhiteSpace(arrow.Text)
            ? new Size(MinimumMemoWidth - 16, MinimumMemoHeight - 12)
            : MeasureMemoText(arrow.Text, maximumTextSize, arrow.FontName, arrow.FontSize);
        var width = Math.Clamp(measured.Width + 16, MinimumMemoWidth, maximumTextSize.Width + 16);
        var height = Math.Clamp(measured.Height + 12, MinimumMemoHeight, maximumTextSize.Height + 12);

        var dx = arrow.End.X - arrow.Start.X;
        var dy = arrow.End.Y - arrow.Start.Y;
        int x;
        int y;
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            var placeRight = dx >= 0;
            if (placeRight && arrow.End.X + width > virtualBounds.Right)
            {
                placeRight = false;
            }
            else if (!placeRight && arrow.End.X - width < virtualBounds.Left)
            {
                placeRight = true;
            }

            x = placeRight ? arrow.End.X : arrow.End.X - width;
            y = arrow.End.Y - height / 2;
        }
        else
        {
            var placeBelow = dy >= 0;
            if (placeBelow && arrow.End.Y + height > virtualBounds.Bottom)
            {
                placeBelow = false;
            }
            else if (!placeBelow && arrow.End.Y - height < virtualBounds.Top)
            {
                placeBelow = true;
            }

            x = arrow.End.X - width / 2;
            y = placeBelow ? arrow.End.Y : arrow.End.Y - height;
        }

        x = Math.Clamp(x, virtualBounds.Left, Math.Max(virtualBounds.Left, virtualBounds.Right - width));
        y = Math.Clamp(y, virtualBounds.Top, Math.Max(virtualBounds.Top, virtualBounds.Bottom - height));
        return new Rectangle(x, y, width, height);
    }

    public static Rectangle GetTextBounds(ScreenText text, Rectangle virtualBounds)
    {
        var maximumTextSize = GetMaximumTextSize(virtualBounds);
        var measured = string.IsNullOrWhiteSpace(text.Text)
            ? new Size(MinimumMemoWidth - 16, MinimumMemoHeight - 12)
            : MeasureMemoText(text.Text, maximumTextSize, text.FontName, text.FontSize);
        var width = Math.Clamp(measured.Width + 16, MinimumMemoWidth, maximumTextSize.Width + 16);
        var height = Math.Clamp(measured.Height + 12, MinimumMemoHeight, maximumTextSize.Height + 12);
        var x = Math.Clamp(text.Location.X, virtualBounds.Left, Math.Max(virtualBounds.Left, virtualBounds.Right - width));
        var y = Math.Clamp(text.Location.Y, virtualBounds.Top, Math.Max(virtualBounds.Top, virtualBounds.Bottom - height));
        return new Rectangle(x, y, width, height);
    }

    public static StringFormat CreateMemoStringFormat()
    {
        return new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.None,
            FormatFlags = StringFormatFlags.LineLimit
        };
    }

    public static Point ConstrainShapeEnd(AnnotationShapeKind kind, Point start, Point end)
    {
        return kind switch
        {
            AnnotationShapeKind.HorizontalLine => new Point(end.X, start.Y),
            AnnotationShapeKind.VerticalLine => new Point(start.X, end.Y),
            _ => end
        };
    }

    public static Rectangle GetShapeBounds(ScreenShape shape)
    {
        if (shape.Kind == AnnotationShapeKind.FilledSquare)
        {
            return new Rectangle(
                shape.Start.X - shape.MarkerSize / 2,
                shape.Start.Y - shape.MarkerSize / 2,
                shape.MarkerSize,
                shape.MarkerSize);
        }

        return NormalizeRectangle(shape.Start, shape.End);
    }

    public static Rectangle NormalizeRectangle(Point first, Point second)
    {
        return Rectangle.FromLTRB(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Max(first.X, second.X),
            Math.Max(first.Y, second.Y));
    }

    public static bool IsShapeLargeEnough(ScreenShape shape)
    {
        if (shape.Kind == AnnotationShapeKind.FilledSquare)
        {
            return shape.MarkerSize > 0;
        }

        if (shape.Kind is AnnotationShapeKind.Rectangle or AnnotationShapeKind.Ellipse)
        {
            var bounds = GetShapeBounds(shape);
            return bounds.Width >= 3 && bounds.Height >= 3;
        }

        var dx = shape.End.X - shape.Start.X;
        var dy = shape.End.Y - shape.Start.Y;
        return dx * dx + dy * dy >= 25;
    }

    private static Size MeasureMemoText(string text, Size maximumSize, string fontName, float fontSize)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        using var font = CreateMemoFont(fontName, fontSize);
        using var format = CreateMemoStringFormat();
        var measured = graphics.MeasureString(text, font, maximumSize, format);
        return new Size(
            Math.Min(maximumSize.Width, (int)Math.Ceiling(measured.Width) + 1),
            Math.Min(maximumSize.Height, (int)Math.Ceiling(measured.Height) + 1));
    }

    private static Size GetMaximumTextSize(Rectangle virtualBounds)
    {
        return new Size(
            Math.Max(MinimumMemoWidth - 16, Math.Min(MaximumMemoWidth, virtualBounds.Width - 32)),
            Math.Max(MinimumMemoHeight - 12, virtualBounds.Height - 32));
    }
}
