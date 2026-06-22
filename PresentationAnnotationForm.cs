using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WindowForm_Move;

public sealed class PresentationAnnotationForm : Form
{
    private readonly Bitmap _frozenScreen;
    private readonly Rectangle _virtualBounds;
    private readonly AnnotationSettings _settings;
    private readonly Func<string> _createCapturePath;
    private readonly List<AnnotationItem> _items = new();
    private readonly Label _noticeLabel = new();
    private AnnotationTool _activeTool;
    private Point? _arrowStart;
    private Point _arrowPreviewEnd;
    private bool _arrowPreviewVisible;
    private bool _erasing;
    private ScreenStroke? _activeStroke;
    private AnnotationArrow? _memoToEdit;
    private Form? _ownedToolbar;

    public PresentationAnnotationForm(
        Bitmap frozenScreen,
        Rectangle virtualBounds,
        AnnotationSettings settings,
        Func<string> createCapturePath)
    {
        _frozenScreen = frozenScreen;
        _virtualBounds = virtualBounds;
        _settings = settings;
        _createCapturePath = createCapturePath;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        Bounds = virtualBounds;

        _noticeLabel.Text = "마킹 중지를 원하시면 ESC 클릭";
        _noticeLabel.AutoSize = false;
        _noticeLabel.Size = new Size(230, 28);
        _noticeLabel.BackColor = Color.FromArgb(28, 28, 28);
        _noticeLabel.ForeColor = Color.White;
        _noticeLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _noticeLabel.TextAlign = ContentAlignment.MiddleCenter;
        _noticeLabel.Visible = false;
        Controls.Add(_noticeLabel);
    }

    public event Action<AnnotationTool>? ToolChanged;
    public event Action? SettingsChanged;

    public void PositionNotice(Rectangle toolbarBounds)
    {
        var localX = toolbarBounds.Left - Left - _noticeLabel.Width - 6;
        var localY = toolbarBounds.Top - Top;
        _noticeLabel.Location = new Point(Math.Max(4, localX), Math.Max(4, localY));
        _noticeLabel.Visible = true;
        _noticeLabel.BringToFront();
    }

    public void AttachToolbar(Form toolbar)
    {
        if (_ownedToolbar == toolbar)
        {
            return;
        }

        DetachToolbar();
        AddOwnedForm(toolbar);
        _ownedToolbar = toolbar;
    }

    public void SetTool(AnnotationTool tool)
    {
        CancelCurrentGesture();
        _activeTool = tool;
        Cursor = tool switch
        {
            AnnotationTool.Dot => Cursors.Cross,
            AnnotationTool.Marker => Cursors.Cross,
            AnnotationTool.Arrow => Cursors.Cross,
            AnnotationTool.Pencil => Cursors.Cross,
            AnnotationTool.Eraser => Cursors.No,
            _ => Cursors.Default
        };
        ToolChanged?.Invoke(tool);
    }

    public void SyncSettings()
    {
        Invalidate();
    }

    public void ChooseMarkerColor()
    {
        using var dialog = new ColorDialog { Color = _settings.MarkerColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings.MarkerColorArgb = dialog.Color.ToArgb();
            _settings.Save();
            SyncSettings();
            SettingsChanged?.Invoke();
        }
    }

    public void ChoosePenColor()
    {
        using var dialog = new ColorDialog { Color = _settings.PenColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings.PenColorArgb = dialog.Color.ToArgb();
            _settings.Save();
            SyncSettings();
            SettingsChanged?.Invoke();
        }
    }

    public void ShowAnnotationSettings()
    {
        using var form = new AnnotationSettingsForm(_settings);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            form.ApplyTo(_settings);
            _settings.Save();
            SyncSettings();
            SettingsChanged?.Invoke();
        }
    }

    public void UndoLast()
    {
        CancelCurrentGesture();
        if (_items.Count > 0)
        {
            _items.RemoveAt(_items.Count - 1);
            Invalidate();
        }
    }

    public void ClearAll()
    {
        CancelCurrentGesture();
        _items.Clear();
        Invalidate();
    }

    public void CaptureSelectedRegion()
    {
        CancelCurrentGesture();
        using var composite = RenderComposite();
        using var selectorImage = (Bitmap)composite.Clone();
        Hide();
        var saved = false;
        try
        {
            using var selector = new CaptureSelectionForm(selectorImage, _virtualBounds);
            if (selector.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            using var result = composite.Clone(selector.SelectedRegion, PixelFormat.Format32bppArgb);
            var outputPath = _createCapturePath();
            result.Save(outputPath, ImageFormat.Png);
            var copied = TryCopyToClipboard(result);
            Show();
            Activate();
            MessageBox.Show(
                this,
                copied
                    ? $"캡처를 저장하고 클립보드에 복사했습니다.\n\n{outputPath}"
                    : $"캡처를 저장했습니다.\n\n{outputPath}",
                "캡처 완료",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            ExplorerFolder.OpenIfNotOpen(Path.GetDirectoryName(outputPath)!);
            saved = true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "캡처 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (saved)
            {
                Close();
            }
            else
            {
                Show();
                Activate();
            }
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawImageUnscaled(_frozenScreen, Point.Empty);
        DrawAnnotations(e.Graphics);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var location = PointToScreen(e.Location);
        if (_activeTool == AnnotationTool.Dot)
        {
            AddDot(location);
        }
        else if (_activeTool == AnnotationTool.Marker)
        {
            AddMarker(location);
        }
        else if (_activeTool == AnnotationTool.Arrow)
        {
            _memoToEdit = FindArrowAt(location);
            if (_memoToEdit is null)
            {
                _arrowStart = location;
                _arrowPreviewEnd = location;
                Capture = true;
            }
        }
        else if (_activeTool == AnnotationTool.Pencil)
        {
            _activeStroke = new ScreenStroke(
                new List<Point> { location },
                _settings.PenColor,
                _settings.PenWidth);
            _items.Add(_activeStroke);
            Capture = true;
        }
        else if (_activeTool == AnnotationTool.Eraser)
        {
            _erasing = true;
            Capture = true;
            EraseAt(location);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var location = PointToScreen(e.Location);
        if (_activeTool == AnnotationTool.Arrow && _arrowStart is not null && e.Button == MouseButtons.Left)
        {
            UpdateArrowPreview(location);
        }
        else if (_activeTool == AnnotationTool.Pencil && _activeStroke is not null && e.Button == MouseButtons.Left)
        {
            var last = _activeStroke.Points[^1];
            if (DistanceSquared(last, location) >= 4F)
            {
                _activeStroke.Points.Add(location);
                Invalidate();
            }
        }
        else if (_activeTool == AnnotationTool.Eraser && _erasing && e.Button == MouseButtons.Left)
        {
            EraseAt(location);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Capture = false;
        if (_memoToEdit is not null)
        {
            var arrow = _memoToEdit;
            _memoToEdit = null;
            EditArrowMemo(arrow, removeOnCancel: false);
        }
        else if (_activeTool == AnnotationTool.Arrow && _arrowStart is not null)
        {
            CompleteArrow(PointToScreen(e.Location));
        }

        if (_activeStroke is not null)
        {
            if (_activeStroke.Points.Count == 1)
            {
                _activeStroke.Points.Add(PointToScreen(e.Location));
            }

            _activeStroke = null;
            Invalidate();
        }

        _erasing = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Escape)
        {
            return;
        }

        if (_arrowStart is not null || _erasing)
        {
            CancelCurrentGesture();
        }
        else
        {
            Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DetachToolbar();
            CancelCurrentGesture();
            _frozenScreen.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DetachToolbar()
    {
        if (_ownedToolbar is null)
        {
            return;
        }

        RemoveOwnedForm(_ownedToolbar);
        _ownedToolbar = null;
    }

    private void AddMarker(Point location)
    {
        _items.Add(new ScreenMarker(_settings.NextMarkerNumber, location, _settings.MarkerColor));
        _settings.NextMarkerNumber = Math.Min(9999, _settings.NextMarkerNumber + 1);
        SyncSettings();
        SettingsChanged?.Invoke();
        Invalidate();
    }

    private void AddDot(Point location)
    {
        _items.Add(new ScreenDot(location, _settings.MarkerColor));
        Invalidate();
    }

    private void UpdateArrowPreview(Point location)
    {
        EraseArrowPreview();
        _arrowPreviewEnd = location;
        if (_arrowStart is not null && _arrowStart.Value != location)
        {
            ControlPaint.DrawReversibleLine(_arrowStart.Value, location, _settings.ArrowColor);
            _arrowPreviewVisible = true;
        }
    }

    private void CompleteArrow(Point end)
    {
        if (_arrowStart is null)
        {
            return;
        }

        var start = _arrowStart.Value;
        EraseArrowPreview();
        _arrowStart = null;
        if (DistanceSquared(start, end) < 25F)
        {
            return;
        }

        var arrow = new AnnotationArrow(start, end, _settings.ArrowColor, _settings.ArrowWidth);
        _items.Add(arrow);
        Invalidate();
        EditArrowMemo(arrow, removeOnCancel: false);
    }

    private void EditArrowMemo(AnnotationArrow arrow, bool removeOnCancel)
    {
        using var form = new ArrowMemoForm(arrow.Text, arrow.End);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            arrow.Text = form.MemoText;
        }
        else if (removeOnCancel)
        {
            _items.Remove(arrow);
        }

        Invalidate();
    }

    private AnnotationArrow? FindArrowAt(Point location)
    {
        return _items
            .OfType<AnnotationArrow>()
            .LastOrDefault(arrow =>
                (!string.IsNullOrWhiteSpace(arrow.Text) &&
                 AnnotationGeometry.GetMemoBounds(arrow, _virtualBounds).Contains(location)) ||
                DistanceToSegment(location, arrow.Start, arrow.End) <= arrow.Width / 2F + 8F);
    }

    private void EraseAt(Point location)
    {
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            if (!HitTest(_items[index], location))
            {
                continue;
            }

            _items.RemoveAt(index);
            Invalidate();
            return;
        }
    }

    private bool HitTest(AnnotationItem item, Point location)
    {
        if (item is ScreenMarker marker)
        {
            var radius = _settings.MarkerSize / 2F + 8F;
            return DistanceSquared(marker.Location, location) <= radius * radius;
        }

        if (item is ScreenDot dot)
        {
            var radius = _settings.MarkerSize / 2F + 8F;
            return DistanceSquared(dot.Location, location) <= radius * radius;
        }

        if (item is ScreenStroke stroke)
        {
            for (var index = 1; index < stroke.Points.Count; index++)
            {
                if (DistanceToSegment(location, stroke.Points[index - 1], stroke.Points[index]) <= stroke.Width / 2F + 8F)
                {
                    return true;
                }
            }

            return stroke.Points.Count == 1 && DistanceSquared(stroke.Points[0], location) <= 64F;
        }

        if (item is not AnnotationArrow arrow)
        {
            return false;
        }

        return AnnotationGeometry.GetMemoBounds(arrow, _virtualBounds).Contains(location) ||
               DistanceToSegment(location, arrow.Start, arrow.End) <= arrow.Width / 2F + 8F;
    }

    private void DrawAnnotations(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        foreach (var item in _items)
        {
            if (item is ScreenMarker marker)
            {
                DrawMarker(graphics, marker);
            }
            else if (item is ScreenDot dot)
            {
                DrawDot(graphics, dot);
            }
            else if (item is AnnotationArrow arrow)
            {
                DrawArrow(graphics, arrow);
            }
            else if (item is ScreenStroke stroke)
            {
                DrawStroke(graphics, stroke);
            }
        }
    }

    private void DrawMarker(Graphics graphics, ScreenMarker marker)
    {
        var center = ToLocal(marker.Location);
        var diameter = _settings.MarkerSize;
        var circle = new Rectangle(center.X - diameter / 2, center.Y - diameter / 2, diameter, diameter);
        using var brush = new SolidBrush(marker.Color);
        graphics.FillEllipse(brush, circle);
        using var font = new Font("Segoe UI", marker.Number >= 100 ? 8F : 10F, FontStyle.Bold);
        TextRenderer.DrawText(
            graphics,
            marker.Number.ToString(),
            font,
            circle,
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void DrawDot(Graphics graphics, ScreenDot dot)
    {
        var center = ToLocal(dot.Location);
        var diameter = _settings.MarkerSize;
        var circle = new Rectangle(center.X - diameter / 2, center.Y - diameter / 2, diameter, diameter);
        using var brush = new SolidBrush(dot.Color);
        graphics.FillEllipse(brush, circle);
    }

    private void DrawArrow(Graphics graphics, AnnotationArrow arrow)
    {
        var start = ToLocal(arrow.Start);
        var end = ToLocal(arrow.End);
        using var cap = new AdjustableArrowCap(5F, 6F, true);
        using var pen = new Pen(arrow.Color, arrow.Width) { CustomEndCap = cap, StartCap = LineCap.Round };
        graphics.DrawLine(pen, start, end);

        if (string.IsNullOrWhiteSpace(arrow.Text))
        {
            return;
        }

        var memo = AnnotationGeometry.GetMemoBounds(arrow, _virtualBounds);
        memo.Offset(-Left, -Top);
        using var back = new SolidBrush(Color.FromArgb(255, 252, 220));
        using var border = new Pen(arrow.Color, 2F);
        graphics.FillRectangle(back, memo);
        graphics.DrawRectangle(border, Rectangle.Inflate(memo, -1, -1));
        var textBounds = Rectangle.Inflate(memo, -8, -6);
        TextRenderer.DrawText(
            graphics,
            arrow.Text,
            SystemFonts.MessageBoxFont,
            textBounds,
            Color.FromArgb(28, 28, 28),
            TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    private void DrawStroke(Graphics graphics, ScreenStroke stroke)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        using var pen = new Pen(stroke.Color, stroke.Width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        var points = stroke.Points.Select(ToLocal).ToArray();
        if (points.Length == 1)
        {
            graphics.DrawEllipse(pen, points[0].X, points[0].Y, 1, 1);
        }
        else
        {
            graphics.DrawLines(pen, points);
        }
    }

    private Bitmap RenderComposite()
    {
        var bitmap = new Bitmap(_frozenScreen.Width, _frozenScreen.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.DrawImageUnscaled(_frozenScreen, Point.Empty);
        DrawAnnotations(graphics);
        return bitmap;
    }

    private Point ToLocal(Point screenPoint)
    {
        return new Point(screenPoint.X - Left, screenPoint.Y - Top);
    }

    private void CancelCurrentGesture()
    {
        EraseArrowPreview();
        _arrowStart = null;
        if (_activeStroke is not null && _activeStroke.Points.Count < 2)
        {
            _items.Remove(_activeStroke);
        }

        _activeStroke = null;
        _erasing = false;
        _memoToEdit = null;
        Capture = false;
    }

    private void EraseArrowPreview()
    {
        if (_arrowPreviewVisible && _arrowStart is not null)
        {
            ControlPaint.DrawReversibleLine(_arrowStart.Value, _arrowPreviewEnd, _settings.ArrowColor);
        }

        _arrowPreviewVisible = false;
    }

    private static float DistanceSquared(Point first, Point second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return dx * dx + dy * dy;
    }

    private static float DistanceToSegment(Point point, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        if (dx == 0 && dy == 0)
        {
            return MathF.Sqrt(DistanceSquared(point, start));
        }

        var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (float)(dx * dx + dy * dy);
        t = Math.Clamp(t, 0F, 1F);
        var nearestX = start.X + t * dx;
        var nearestY = start.Y + t * dy;
        var offsetX = point.X - nearestX;
        var offsetY = point.Y - nearestY;
        return MathF.Sqrt(offsetX * offsetX + offsetY * offsetY);
    }

    private static bool TryCopyToClipboard(Bitmap image)
    {
        try
        {
            using var clipboardImage = (Bitmap)image.Clone();
            Clipboard.SetImage(clipboardImage);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
