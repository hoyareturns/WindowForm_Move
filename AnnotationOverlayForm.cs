namespace WindowForm_Move;

public sealed class AnnotationOverlayForm : Form
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_LAYERED = 0x00080000;

    private readonly Func<IReadOnlyList<AnnotationItem>> _getItems;
    private readonly Action<Point> _addMarker;
    private readonly Action<Point> _beginStroke;
    private readonly Action<Point> _appendStroke;
    private readonly Action _endStroke;
    private readonly Action<Point> _eraseAt;
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
        Screen screen,
        Func<IReadOnlyList<AnnotationItem>> getItems,
        Action<Point> addMarker,
        Action<Point> beginStroke,
        Action<Point> appendStroke,
        Action endStroke,
        Action<Point> eraseAt,
        Func<AnnotationSettings> getSettings)
    {
        ScreenDeviceName = screen.DeviceName;
        _getItems = getItems;
        _addMarker = addMarker;
        _beginStroke = beginStroke;
        _appendStroke = appendStroke;
        _endStroke = endStroke;
        _eraseAt = eraseAt;
        _getSettings = getSettings;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Fuchsia;
        TransparencyKey = Color.Fuchsia;
        DoubleBuffered = true;
        Bounds = screen.Bounds;
        Cursor = Cursors.Cross;
    }

    public string ScreenDeviceName { get; }

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

    public void RefreshAnnotations()
    {
        UpdateVisibility();
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_activeTool == AnnotationTool.Marker)
        {
            _addMarker(PointToScreen(e.Location));
        }
        else if (_activeTool == AnnotationTool.Pen)
        {
            Capture = true;
            _beginStroke(PointToScreen(e.Location));
        }
        else if (_activeTool == AnnotationTool.Eraser)
        {
            Capture = true;
            _eraseAt(PointToScreen(e.Location));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_activeTool == AnnotationTool.Pen && Capture && e.Button == MouseButtons.Left)
        {
            _appendStroke(PointToScreen(e.Location));
        }
        else if (_activeTool == AnnotationTool.Eraser && Capture && e.Button == MouseButtons.Left)
        {
            _eraseAt(PointToScreen(e.Location));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_activeTool == AnnotationTool.Pen && Capture && e.Button == MouseButtons.Left)
        {
            Capture = false;
            _endStroke();
        }
        else if (_activeTool == AnnotationTool.Eraser && Capture && e.Button == MouseButtons.Left)
        {
            Capture = false;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        foreach (var item in _getItems())
        {
            if (item is ScreenMarker marker && Bounds.Contains(marker.Location))
            {
                DrawMarker(e.Graphics, marker);
            }
            else if (item is DrawingStroke stroke)
            {
                DrawStroke(e.Graphics, stroke);
            }
        }
    }

    private void UpdateVisibility()
    {
        var hasAnnotations = _getItems().Any(item => item switch
        {
            ScreenMarker marker => Bounds.Contains(marker.Location),
            DrawingStroke stroke => stroke.Points.Any(Bounds.Contains),
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

        using var shadowBrush = new SolidBrush(Color.FromArgb(130, 0, 0, 0));
        var shadow = new Rectangle(circle.X + 2, circle.Y + 2, circle.Width, circle.Height);
        graphics.FillEllipse(shadowBrush, shadow);
        using var fillBrush = new SolidBrush(Color.FromArgb(225, settings.MarkerColor));
        using var borderPen = new Pen(Color.White, 2F);
        graphics.FillEllipse(fillBrush, circle);
        graphics.DrawEllipse(borderPen, circle);

        using var font = new Font("Segoe UI", marker.Number >= 100 ? 8F : 10F, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        var text = marker.Number.ToString();
        var size = graphics.MeasureString(text, font);
        graphics.DrawString(text, font, textBrush, center.X - size.Width / 2, center.Y - size.Height / 2);
    }

    private void DrawStroke(Graphics graphics, DrawingStroke stroke)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        var points = stroke.Points.Select(PointToClient).ToArray();
        using var pen = new Pen(stroke.Color, stroke.Width)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };

        if (points.Length == 1)
        {
            graphics.DrawEllipse(pen, points[0].X, points[0].Y, 1, 1);
        }
        else
        {
            graphics.DrawLines(pen, points);
        }
    }
}

public abstract record AnnotationItem;

public sealed record ScreenMarker(int Number, Point Location) : AnnotationItem;

public sealed record DrawingStroke(List<Point> Points, Color Color, float Width) : AnnotationItem;

public enum AnnotationTool
{
    None,
    Marker,
    Pen,
    Eraser
}
