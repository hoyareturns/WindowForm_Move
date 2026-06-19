namespace WindowForm_Move;

public static class AnnotationGeometry
{
    private const int MinimumMemoWidth = 90;
    private const int MinimumMemoHeight = 38;
    private const int MaximumMemoWidth = 300;
    private const int MaximumMemoHeight = 160;

    public static Rectangle GetMemoBounds(AnnotationArrow arrow, Rectangle virtualBounds)
    {
        var measured = string.IsNullOrWhiteSpace(arrow.Text)
            ? new Size(MinimumMemoWidth - 16, MinimumMemoHeight - 12)
            : TextRenderer.MeasureText(
                arrow.Text,
                SystemFonts.MessageBoxFont,
                new Size(MaximumMemoWidth - 16, MaximumMemoHeight - 12),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        var width = Math.Clamp(measured.Width + 16, MinimumMemoWidth, MaximumMemoWidth);
        var height = Math.Clamp(measured.Height + 12, MinimumMemoHeight, MaximumMemoHeight);

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
}
