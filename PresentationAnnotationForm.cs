using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WindowForm_Move;

public sealed class PresentationAnnotationForm : Form
{
    private readonly Bitmap _frozenScreen;
    private readonly Rectangle _virtualBounds;
    private readonly bool _showTargetBoundary;
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
    private ScreenShape? _activeShape;
    private int _activeShapeIndex = -1;
    private AnnotationArrow? _memoToEdit;
    private int _movingItemIndex = -1;
    private Point _moveStart;
    private AnnotationItem? _moveOriginal;
    private Form? _ownedToolbar;

    public PresentationAnnotationForm(
        Bitmap frozenScreen,
        Rectangle virtualBounds,
        bool showTargetBoundary,
        AnnotationSettings settings,
        Func<string> createCapturePath)
    {
        _frozenScreen = frozenScreen;
        _virtualBounds = virtualBounds;
        _showTargetBoundary = showTargetBoundary;
        _settings = settings;
        _createCapturePath = createCapturePath;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        Bounds = virtualBounds;

        _noticeLabel.Text = string.Empty;
        _noticeLabel.AutoSize = false;
        _noticeLabel.Size = new Size(230, 28);
        _noticeLabel.BackColor = Color.FromArgb(28, 28, 28);
        _noticeLabel.ForeColor = Color.White;
        _noticeLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _noticeLabel.TextAlign = ContentAlignment.MiddleCenter;
        _noticeLabel.Visible = false;
        Controls.Add(_noticeLabel);
        AddTargetGuide();
    }

    public event Action<AnnotationTool>? ToolChanged;
    public event Action? SettingsChanged;
    public event Action? SettingsApplied;
    public event Action<string>? StatusChanged;

    public void PositionNotice(Rectangle toolbarBounds)
    {
        if (string.IsNullOrWhiteSpace(_noticeLabel.Text))
        {
            _noticeLabel.Visible = false;
            return;
        }

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
            AnnotationTool.Text => Cursors.IBeam,
            AnnotationTool.FilledSquare => Cursors.Cross,
            AnnotationTool.DoubleArrow => Cursors.Cross,
            AnnotationTool.Rectangle => Cursors.Cross,
            AnnotationTool.Ellipse => Cursors.Cross,
            AnnotationTool.Line => Cursors.Cross,
            AnnotationTool.HorizontalLine => Cursors.Cross,
            AnnotationTool.VerticalLine => Cursors.Cross,
            AnnotationTool.Moving => Cursors.SizeAll,
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
            SettingsApplied?.Invoke();
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
        using var form = new AnnotationSettingsForm(_settings, ApplySettings);
        form.ShowDialog(this);
    }

    private bool ApplySettings()
    {
        if (!StartupManager.SetEnabled(_settings.AutoStartWithWindows))
        {
            MessageBox.Show(
                this,
                "Windows 자동 실행 설정을 변경하지 못했습니다.",
                "Smart_Window 설정",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        _settings.Save();
        SyncSettings();
        SettingsChanged?.Invoke();
        SettingsApplied?.Invoke();
        return true;
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
        try
        {
            using var selector = new CaptureSelectionForm(selectorImage, _virtualBounds);
            if (selector.ShowDialog() != DialogResult.OK)
            {
                StatusChanged?.Invoke($"{DateTime.Now:HH:mm:ss} 캡처 취소");
                return;
            }

            using var result = composite.Clone(selector.SelectedRegion, PixelFormat.Format32bppArgb);
            var outputPath = _createCapturePath();
            result.Save(outputPath, ImageFormat.Png);
            var copied = TryCopyToClipboard(result);
            StatusChanged?.Invoke(copied
                ? $"{DateTime.Now:HH:mm:ss} 저장·클립보드 복사 완료: {Path.GetFileName(outputPath)}"
                : $"{DateTime.Now:HH:mm:ss} 저장 완료 / 클립보드 복사 실패: {Path.GetFileName(outputPath)}");
        }
        catch (Exception exception)
        {
            StatusChanged?.Invoke($"{DateTime.Now:HH:mm:ss} 캡처 오류: {exception.Message}");
        }
        finally
        {
            Show();
            Activate();
            if (_ownedToolbar is not null && !_ownedToolbar.IsDisposed)
            {
                _ownedToolbar.Show();
                _ownedToolbar.BringToFront();
            }
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        SetWindowPos(
            Handle,
            HwndTopMost,
            _virtualBounds.Left,
            _virtualBounds.Top,
            _virtualBounds.Width,
            _virtualBounds.Height,
            SwpShowWindow);
        Activate();
        Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawImageUnscaled(_frozenScreen, Point.Empty);
        DrawAnnotations(e.Graphics);
        if (_showTargetBoundary)
        {
            using var boundary = new Pen(Color.FromArgb(0, 170, 230), 3F)
            {
                Alignment = PenAlignment.Inset
            };
            e.Graphics.DrawRectangle(
                boundary,
                1,
                1,
                Math.Max(1, ClientSize.Width - 3),
                Math.Max(1, ClientSize.Height - 3));
        }
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
        else if (TryGetShapeKind(_activeTool, out var shapeKind))
        {
            if (shapeKind == AnnotationShapeKind.FilledSquare)
            {
                AddFilledSquare(location);
            }
            else
            {
                StartShape(shapeKind, location);
            }
        }
        else if (_activeTool == AnnotationTool.Text)
        {
            var text = FindTextAt(location);
            if (text is null)
            {
                text = new ScreenText(location, _settings.PenColor)
                {
                    FontName = _settings.MemoFontName,
                    FontSize = _settings.MemoFontSize
                };
                _items.Add(text);
                EditText(text, removeOnCancel: true);
            }
            else
            {
                EditText(text, removeOnCancel: false);
            }
        }
        else if (_activeTool == AnnotationTool.Moving)
        {
            _movingItemIndex = FindItemIndexAt(location);
            if (_movingItemIndex >= 0)
            {
                _moveStart = location;
                _moveOriginal = _items[_movingItemIndex];
                Capture = true;
            }
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
        else if (_activeShape is not null && e.Button == MouseButtons.Left)
        {
            UpdateShape(location);
        }
        else if (_activeTool == AnnotationTool.Moving && _movingItemIndex >= 0 &&
                 _moveOriginal is not null && e.Button == MouseButtons.Left)
        {
            var offset = new Size(location.X - _moveStart.X, location.Y - _moveStart.Y);
            _items[_movingItemIndex] = TranslateItem(_moveOriginal, offset);
            Invalidate();
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

        CompleteShape(PointToScreen(e.Location));
        _movingItemIndex = -1;
        _moveOriginal = null;

        _erasing = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Escape)
        {
            return;
        }

        if (_arrowStart is not null || _activeStroke is not null || _activeShape is not null ||
            _movingItemIndex >= 0 || _erasing)
        {
            CancelCurrentGesture();
        }
        e.SuppressKeyPress = true;
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
        _items.Add(new ScreenMarker(
            _settings.NextMarkerNumber,
            location,
            _settings.MarkerColor,
            _settings.MarkerSize));
        _settings.NextMarkerNumber = Math.Min(9999, _settings.NextMarkerNumber + 1);
        SyncSettings();
        SettingsChanged?.Invoke();
        Invalidate();
    }

    private void AddTargetGuide()
    {
        var guide = new ScreenText(_virtualBounds.Location, Color.Red)
        {
            Text = "여기가 마킹 모니터입니다.\n이 마킹을 지우고 작업하세요.",
            FontName = "맑은 고딕",
            FontSize = 10F
        };
        var bounds = AnnotationGeometry.GetTextBounds(guide, _virtualBounds);
        guide = guide with
        {
            Location = new Point(
                _virtualBounds.Left + Math.Max(0, (_virtualBounds.Width - bounds.Width) / 2),
                _virtualBounds.Top + 16)
        };
        _items.Add(guide);
    }

    private void AddDot(Point location)
    {
        _items.Add(new ScreenDot(location, _settings.MarkerColor, _settings.MarkerSize));
        Invalidate();
    }

    private void AddFilledSquare(Point location)
    {
        _items.Add(new ScreenShape(
            AnnotationShapeKind.FilledSquare,
            location,
            location,
            _settings.MarkerColor,
            _settings.PenWidth,
            _settings.MarkerSize));
        Invalidate();
    }

    private void StartShape(AnnotationShapeKind kind, Point location)
    {
        _activeShape = new ScreenShape(
            kind,
            location,
            location,
            _settings.PenColor,
            _settings.PenWidth,
            _settings.MarkerSize);
        _activeShapeIndex = _items.Count;
        _items.Add(_activeShape);
        Capture = true;
        Invalidate();
    }

    private void UpdateShape(Point location)
    {
        if (_activeShape is null || _activeShapeIndex < 0 || _activeShapeIndex >= _items.Count)
        {
            return;
        }

        var end = AnnotationGeometry.ConstrainShapeEnd(_activeShape.Kind, _activeShape.Start, location);
        _activeShape = _activeShape with { End = end };
        _items[_activeShapeIndex] = _activeShape;
        Invalidate();
    }

    private void CompleteShape(Point location)
    {
        if (_activeShape is null)
        {
            return;
        }

        UpdateShape(location);
        if (!AnnotationGeometry.IsShapeLargeEnough(_activeShape) &&
            _activeShapeIndex >= 0 && _activeShapeIndex < _items.Count)
        {
            _items.RemoveAt(_activeShapeIndex);
        }

        _activeShape = null;
        _activeShapeIndex = -1;
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

        var arrow = new AnnotationArrow(start, end, _settings.ArrowColor, _settings.ArrowWidth)
        {
            FontName = _settings.MemoFontName,
            FontSize = _settings.MemoFontSize
        };
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

    private ScreenText? FindTextAt(Point location)
    {
        return _items
            .OfType<ScreenText>()
            .LastOrDefault(text => AnnotationGeometry.GetTextBounds(text, _virtualBounds).Contains(location));
    }

    private void EditText(ScreenText text, bool removeOnCancel)
    {
        using var form = new ArrowMemoForm(text.Text, text.Location, textOnly: true);
        if (form.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(form.MemoText))
        {
            text.Text = form.MemoText;
        }
        else if (removeOnCancel || string.IsNullOrWhiteSpace(form.MemoText))
        {
            _items.Remove(text);
        }

        Invalidate();
    }

    private int FindItemIndexAt(Point location)
    {
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            if (HitTest(_items[index], location))
            {
                return index;
            }
        }

        return -1;
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
            var radius = marker.Size / 2F + 8F;
            return DistanceSquared(marker.Location, location) <= radius * radius;
        }

        if (item is ScreenDot dot)
        {
            var radius = dot.Size / 2F + 8F;
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

        if (item is ScreenShape shape)
        {
            return HitTestShape(shape, location);
        }

        if (item is ScreenText text)
        {
            return AnnotationGeometry.GetTextBounds(text, _virtualBounds).Contains(location);
        }

        if (item is not AnnotationArrow arrow)
        {
            return false;
        }

        return (!string.IsNullOrWhiteSpace(arrow.Text) &&
                AnnotationGeometry.GetMemoBounds(arrow, _virtualBounds).Contains(location)) ||
               DistanceToSegment(location, arrow.Start, arrow.End) <= arrow.Width / 2F + 8F;
    }

    private void DrawAnnotations(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
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
            else if (item is ScreenShape shape)
            {
                DrawShape(graphics, shape);
            }
            else if (item is ScreenText text)
            {
                DrawText(graphics, text);
            }
        }
    }

    private void DrawMarker(Graphics graphics, ScreenMarker marker)
    {
        var center = ToLocal(marker.Location);
        var diameter = marker.Size;
        var circle = new Rectangle(center.X - diameter / 2, center.Y - diameter / 2, diameter, diameter);
        using var brush = new SolidBrush(marker.Color);
        graphics.FillEllipse(brush, circle);
        var digitCount = marker.Number.ToString().Length;
        var fontSize = Math.Max(1F, diameter * (digitCount switch
        {
            1 => 0.46F,
            2 => 0.38F,
            3 => 0.30F,
            _ => 0.24F
        }));
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
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
        var diameter = dot.Size;
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
        DrawMemoText(graphics, arrow.Text, textBounds, arrow.FontName, arrow.FontSize);
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

    private void DrawShape(Graphics graphics, ScreenShape shape)
    {
        var bounds = AnnotationGeometry.GetShapeBounds(shape);
        bounds.Offset(-Left, -Top);
        var start = ToLocal(shape.Start);
        var end = ToLocal(shape.End);

        if (shape.Kind == AnnotationShapeKind.FilledSquare)
        {
            using var brush = new SolidBrush(shape.Color);
            graphics.FillRectangle(brush, bounds);
            return;
        }

        using var pen = new Pen(shape.Color, shape.Width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        if (shape.Kind == AnnotationShapeKind.DoubleArrow)
        {
            using var startCap = new AdjustableArrowCap(5F, 6F, true);
            using var endCap = new AdjustableArrowCap(5F, 6F, true);
            pen.CustomStartCap = startCap;
            pen.CustomEndCap = endCap;
            graphics.DrawLine(pen, start, end);
        }
        else if (shape.Kind == AnnotationShapeKind.Rectangle)
        {
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                graphics.DrawRectangle(pen, bounds);
            }
        }
        else if (shape.Kind == AnnotationShapeKind.Ellipse)
        {
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                graphics.DrawEllipse(pen, bounds);
            }
        }
        else
        {
            graphics.DrawLine(pen, start, end);
        }
    }

    private void DrawText(Graphics graphics, ScreenText text)
    {
        var bounds = AnnotationGeometry.GetTextBounds(text, _virtualBounds);
        bounds.Offset(-Left, -Top);
        using var back = new SolidBrush(Color.FromArgb(255, 252, 220));
        using var border = new Pen(text.Color, 2F);
        graphics.FillRectangle(back, bounds);
        graphics.DrawRectangle(border, Rectangle.Inflate(bounds, -1, -1));
        var textBounds = Rectangle.Inflate(bounds, -8, -6);
        DrawMemoText(graphics, text.Text, textBounds, text.FontName, text.FontSize);
    }

    private static void DrawMemoText(
        Graphics graphics,
        string text,
        Rectangle bounds,
        string fontName,
        float fontSize)
    {
        var previousHint = graphics.TextRenderingHint;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        using var brush = new SolidBrush(Color.Black);
        using var font = AnnotationGeometry.CreateMemoFont(fontName, fontSize);
        using var format = AnnotationGeometry.CreateMemoStringFormat();
        graphics.DrawString(text, font, brush, bounds, format);
        graphics.TextRenderingHint = previousHint;
    }

    private static AnnotationItem TranslateItem(AnnotationItem item, Size offset)
    {
        return item switch
        {
            ScreenMarker marker => marker with { Location = Point.Add(marker.Location, offset) },
            ScreenDot dot => dot with { Location = Point.Add(dot.Location, offset) },
            ScreenStroke stroke => stroke with
            {
                Points = stroke.Points.Select(point => Point.Add(point, offset)).ToList()
            },
            ScreenShape shape => shape with
            {
                Start = Point.Add(shape.Start, offset),
                End = Point.Add(shape.End, offset)
            },
            AnnotationArrow arrow => new AnnotationArrow(
                Point.Add(arrow.Start, offset),
                Point.Add(arrow.End, offset),
                arrow.Color,
                arrow.Width)
            {
                Text = arrow.Text,
                FontName = arrow.FontName,
                FontSize = arrow.FontSize
            },
            ScreenText text => new ScreenText(Point.Add(text.Location, offset), text.Color)
            {
                Text = text.Text,
                FontName = text.FontName,
                FontSize = text.FontSize
            },
            _ => item
        };
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
        if (_activeShape is not null && _activeShapeIndex >= 0 && _activeShapeIndex < _items.Count)
        {
            _items.RemoveAt(_activeShapeIndex);
            Invalidate();
        }

        _activeShape = null;
        _activeShapeIndex = -1;
        if (_movingItemIndex >= 0 && _moveOriginal is not null && _movingItemIndex < _items.Count)
        {
            _items[_movingItemIndex] = _moveOriginal;
            Invalidate();
        }

        _movingItemIndex = -1;
        _moveOriginal = null;
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

    private static bool TryGetShapeKind(AnnotationTool tool, out AnnotationShapeKind kind)
    {
        kind = tool switch
        {
            AnnotationTool.FilledSquare => AnnotationShapeKind.FilledSquare,
            AnnotationTool.DoubleArrow => AnnotationShapeKind.DoubleArrow,
            AnnotationTool.Rectangle => AnnotationShapeKind.Rectangle,
            AnnotationTool.Ellipse => AnnotationShapeKind.Ellipse,
            AnnotationTool.Line => AnnotationShapeKind.Line,
            AnnotationTool.HorizontalLine => AnnotationShapeKind.HorizontalLine,
            AnnotationTool.VerticalLine => AnnotationShapeKind.VerticalLine,
            _ => default
        };
        return tool is AnnotationTool.FilledSquare or
            AnnotationTool.DoubleArrow or
            AnnotationTool.Rectangle or
            AnnotationTool.Ellipse or
            AnnotationTool.Line or
            AnnotationTool.HorizontalLine or
            AnnotationTool.VerticalLine;
    }

    private static bool HitTestShape(ScreenShape shape, Point location)
    {
        var tolerance = shape.Width / 2F + 8F;
        if (shape.Kind == AnnotationShapeKind.FilledSquare)
        {
            return Rectangle.Inflate(AnnotationGeometry.GetShapeBounds(shape), 8, 8).Contains(location);
        }

        if (shape.Kind == AnnotationShapeKind.Ellipse)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(AnnotationGeometry.GetShapeBounds(shape));
            using var pen = new Pen(Color.Black, Math.Max(1F, tolerance * 2F));
            return path.IsOutlineVisible(location, pen);
        }

        if (shape.Kind == AnnotationShapeKind.Rectangle)
        {
            var bounds = AnnotationGeometry.GetShapeBounds(shape);
            var topLeft = new Point(bounds.Left, bounds.Top);
            var topRight = new Point(bounds.Right, bounds.Top);
            var bottomRight = new Point(bounds.Right, bounds.Bottom);
            var bottomLeft = new Point(bounds.Left, bounds.Bottom);
            return DistanceToSegment(location, topLeft, topRight) <= tolerance ||
                   DistanceToSegment(location, topRight, bottomRight) <= tolerance ||
                   DistanceToSegment(location, bottomRight, bottomLeft) <= tolerance ||
                   DistanceToSegment(location, bottomLeft, topLeft) <= tolerance;
        }

        return DistanceToSegment(location, shape.Start, shape.End) <= tolerance;
    }

    private static bool TryCopyToClipboard(Bitmap image)
    {
        try
        {
            using var clipboardImage = (Bitmap)image.Clone();
            Clipboard.SetDataObject(clipboardImage, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static readonly IntPtr HwndTopMost = new(-1);
    private const uint SwpShowWindow = 0x0040;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
