namespace WindowForm_Move;

public sealed class AnnotationOverlayForm : Form
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_LAYERED = 0x00080000;

    private readonly Func<IReadOnlyList<AnnotationItem>> _getItems;
    private readonly Func<AnnotationSettings> _getSettings;
    private AnnotationTool _activeTool;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED;
            cp.ExStyle |= WS_EX_TRANSPARENT;

            return cp;
        }
    }

    public AnnotationOverlayForm(
        Rectangle virtualBounds,
        Func<IReadOnlyList<AnnotationItem>> getItems,
        Func<AnnotationSettings> getSettings)
    {
        _getItems = getItems;
        _getSettings = getSettings;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Fuchsia;
        TransparencyKey = Color.Fuchsia;
        DoubleBuffered = true;
        Bounds = virtualBounds;
        Cursor = Cursors.Cross;
    }

    public void UpdateVirtualBounds(Rectangle virtualBounds)
    {
        if (Bounds != virtualBounds)
        {
            Bounds = virtualBounds;
        }
    }

    public void SetActiveTool(AnnotationTool tool)
    {
        if (_activeTool == tool)
        {
            return;
        }

        _activeTool = tool;
        if (IsHandleCreated)
        {
            RecreateHandle();
        }

        Cursor = tool == AnnotationTool.None ? Cursors.Default : Cursors.Cross;
        UpdateVisibility();
    }

    public void RefreshAnnotations(Rectangle? screenArea = null)
    {
        UpdateVisibility();
        if (screenArea is null)
        {
            Invalidate();
            return;
        }

        var localArea = screenArea.Value;
        localArea.Offset(-Left, -Top);
        localArea.Intersect(ClientRectangle);
        if (!localArea.IsEmpty)
        {
            Invalidate(localArea);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

        foreach (var item in _getItems())
        {
            if (item is ScreenMarker marker && Bounds.Contains(marker.Location))
            {
                DrawMarker(e.Graphics, marker);
            }
            else if (item is AnnotationArrow arrow)
            {
                DrawArrow(e.Graphics, arrow);
            }
        }
    }

    private void UpdateVisibility()
    {
        var hasAnnotations = _getItems().Any(item => item switch
        {
            ScreenMarker marker => Bounds.Contains(marker.Location),
            AnnotationArrow arrow => Bounds.Contains(arrow.Start) || Bounds.Contains(arrow.End),
            _ => false
        });
        if (_activeTool != AnnotationTool.None || hasAnnotations)
        {
            if (!Visible)
            {
                Show();
            }
        }
        else
        {
            Hide();
        }
    }

    private void DrawMarker(Graphics graphics, ScreenMarker marker)
    {
        var center = PointToClient(marker.Location);
        var settings = _getSettings();
        var diameter = settings.MarkerSize;
        var circle = new Rectangle(center.X - diameter / 2, center.Y - diameter / 2, diameter, diameter);

        using var fillBrush = new SolidBrush(settings.MarkerColor);
        graphics.FillEllipse(fillBrush, circle);

        using var font = new Font("Segoe UI", marker.Number >= 100 ? 8F : 10F, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        var text = marker.Number.ToString();
        var size = graphics.MeasureString(text, font);
        graphics.DrawString(text, font, textBrush, center.X - size.Width / 2, center.Y - size.Height / 2);
    }

    private void DrawArrow(Graphics graphics, AnnotationArrow arrow)
    {
        var start = PointToClient(arrow.Start);
        var end = PointToClient(arrow.End);
        if (arrow.IsPending && start == end)
        {
            using var startBrush = new SolidBrush(arrow.Color);
            graphics.FillEllipse(startBrush, start.X - 4, start.Y - 4, 8, 8);
            return;
        }

        using var arrowCap = new System.Drawing.Drawing2D.AdjustableArrowCap(5F, 6F, true);
        using var pen = new Pen(arrow.Color, arrow.Width)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            CustomEndCap = arrowCap,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        graphics.DrawLine(pen, start, end);

        if (arrow.IsPending)
        {
            return;
        }

        var memoBounds = AnnotationGeometry.GetMemoBounds(arrow, Bounds);
        memoBounds.Offset(-Left, -Top);
        using var memoBack = new SolidBrush(Color.FromArgb(255, 252, 220));
        using var memoBorder = new Pen(arrow.Color, 2F);
        graphics.FillRectangle(memoBack, memoBounds);
        graphics.DrawRectangle(memoBorder, Rectangle.Inflate(memoBounds, -1, -1));

        var textBounds = Rectangle.Inflate(memoBounds, -8, -6);
        using var font = new Font("Segoe UI", 10F, FontStyle.Regular);
        using var textBrush = new SolidBrush(Color.FromArgb(28, 28, 28));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisWord
        };
        graphics.DrawString(arrow.Text, font, textBrush, textBounds, format);
    }
}

public abstract record AnnotationItem;

public sealed record ScreenMarker(int Number, Point Location) : AnnotationItem;

public sealed record AnnotationArrow : AnnotationItem
{
    public AnnotationArrow(Point start, Point end, Color color, float width)
    {
        Start = start;
        End = end;
        Color = color;
        Width = width;
    }

    public Point Start { get; }
    public Point End { get; set; }
    public Color Color { get; }
    public float Width { get; }
    public string Text { get; set; } = string.Empty;
    public bool IsPending { get; set; } = true;
}

public enum AnnotationTool
{
    None,
    Marker,
    Arrow,
    Eraser
}
