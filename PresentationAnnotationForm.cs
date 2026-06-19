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
    private readonly Panel _toolWindow = new();
    private readonly FlowLayoutPanel _toolRow = new();
    private readonly ToolTip _toolTip = new() { ShowAlways = true, InitialDelay = 300 };
    private readonly Button _markerColorButton;
    private readonly NumericUpDown _markerNumberInput;
    private readonly Button _markerButton;
    private readonly Button _arrowButton;
    private readonly Button _eraserButton;
    private AnnotationTool _activeTool;
    private Point? _arrowStart;
    private Point _arrowPreviewEnd;
    private bool _arrowPreviewVisible;
    private bool _erasing;
    private AnnotationArrow? _memoToEdit;

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

        _markerColorButton = CreateToolButton(string.Empty, 28, ChooseMarkerColor, "마커 색상");
        _markerNumberInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 9999,
            Value = Math.Clamp(settings.NextMarkerNumber, 1, 9999),
            Size = new Size(52, 23),
            Margin = new Padding(1, 2, 1, 1),
            TextAlign = HorizontalAlignment.Center,
            Font = new Font("Segoe UI", 8F),
            BorderStyle = BorderStyle.FixedSingle
        };
        _markerNumberInput.ValueChanged += (_, _) =>
        {
            _settings.NextMarkerNumber = (int)_markerNumberInput.Value;
            SettingsChanged?.Invoke();
        };
        _toolTip.SetToolTip(_markerNumberInput, "다음 마커 번호");

        _markerButton = CreateToolButton("①", 28, () => SetTool(AnnotationTool.Marker), "번호 마커");
        _arrowButton = CreateToolButton("↗", 28, () => SetTool(AnnotationTool.Arrow), "화살표와 메모");
        _eraserButton = CreateToolButton("E", 28, () => SetTool(AnnotationTool.Eraser), "마커 또는 화살표 지우기");

        BuildToolWindow();
    }

    public event Action<AnnotationTool>? ToolChanged;
    public event Action? SettingsChanged;

    public void SetTool(AnnotationTool tool)
    {
        CancelCurrentGesture();
        _activeTool = tool;
        Cursor = tool switch
        {
            AnnotationTool.Marker => Cursors.Cross,
            AnnotationTool.Arrow => Cursors.Cross,
            AnnotationTool.Eraser => Cursors.No,
            _ => Cursors.Default
        };
        SyncToolButtons();
        ToolChanged?.Invoke(tool);
    }

    public void SyncSettings()
    {
        _markerColorButton.BackColor = _settings.MarkerColor;
        var number = Math.Clamp(_settings.NextMarkerNumber, 1, 9999);
        if (_markerNumberInput.Value != number)
        {
            _markerNumberInput.Value = number;
        }

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
        PositionToolWindow();
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
        if (e.Button != MouseButtons.Left || _toolWindow.Bounds.Contains(e.Location))
        {
            return;
        }

        var location = PointToScreen(e.Location);
        if (_activeTool == AnnotationTool.Marker)
        {
            AddMarker(location);
        }
        else if (_activeTool == AnnotationTool.Arrow)
        {
            _memoToEdit = FindMemoAt(location);
            if (_memoToEdit is null)
            {
                _arrowStart = location;
                _arrowPreviewEnd = location;
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
            CancelCurrentGesture();
            _toolTip.Dispose();
            _frozenScreen.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildToolWindow()
    {
        _toolWindow.BackColor = Color.FromArgb(245, 245, 245);
        _toolWindow.BorderStyle = BorderStyle.FixedSingle;
        _toolWindow.Size = new Size(430, 54);

        var title = new Label
        {
            Text = "프레젠테이션 도구",
            AutoSize = false,
            Location = new Point(6, 2),
            Size = new Size(380, 18),
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(25, 90, 120)
        };
        _toolWindow.Controls.Add(title);

        var closeButton = CreateToolButton("×", 28, Close, "프레젠테이션 모드 종료");
        closeButton.Location = new Point(_toolWindow.Width - 30, 0);
        closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeButton.Margin = Padding.Empty;
        closeButton.Height = 20;
        _toolWindow.Controls.Add(closeButton);

        _toolRow.Location = new Point(2, 21);
        _toolRow.Size = new Size(_toolWindow.Width - 4, 29);
        _toolRow.FlowDirection = FlowDirection.LeftToRight;
        _toolRow.WrapContents = false;
        _toolRow.BackColor = Color.FromArgb(245, 245, 245);
        _toolRow.Controls.Add(_markerColorButton);
        _toolRow.Controls.Add(_markerNumberInput);
        _toolRow.Controls.Add(_markerButton);
        _toolRow.Controls.Add(_arrowButton);
        _toolRow.Controls.Add(_eraserButton);
        _toolRow.Controls.Add(CreateToolButton("↶", 28, UndoLast, "마지막 작업 되돌리기"));
        _toolRow.Controls.Add(CreateToolButton("AC", 34, ClearAll, "모든 주석 지우기"));
        _toolRow.Controls.Add(CreateToolButton("□", 28, CaptureSelectedRegion, "영역 캡처"));
        _toolRow.Controls.Add(CreateToolButton("⚙", 28, ShowAnnotationSettings, "설정"));
        _toolWindow.Controls.Add(_toolRow);
        Controls.Add(_toolWindow);

        SyncSettings();
        SyncToolButtons();
    }

    private Button CreateToolButton(string text, int width, Action action, string toolTip)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(width, 24),
            Margin = new Padding(1, 1, 1, 1),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(245, 245, 245),
            ForeColor = Color.FromArgb(28, 28, 28),
            Font = new Font("Segoe UI Symbol", 9F, FontStyle.Bold),
            TabStop = false
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(190, 190, 190);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 232, 242);
        button.Click += (_, _) => action();
        _toolTip.SetToolTip(button, toolTip);
        return button;
    }

    private void PositionToolWindow()
    {
        var primary = Screen.PrimaryScreen?.Bounds ?? _virtualBounds;
        var localX = primary.Left - Left + (primary.Width - _toolWindow.Width) / 2;
        var localY = primary.Top - Top + 8;
        _toolWindow.Location = new Point(localX, localY);
        _toolWindow.BringToFront();
    }

    private void SyncToolButtons()
    {
        foreach (var pair in new[]
        {
            (_markerButton, AnnotationTool.Marker),
            (_arrowButton, AnnotationTool.Arrow),
            (_eraserButton, AnnotationTool.Eraser)
        })
        {
            pair.Item1.BackColor = _activeTool == pair.Item2
                ? Color.FromArgb(190, 218, 238)
                : Color.FromArgb(245, 245, 245);
        }
    }

    private void AddMarker(Point location)
    {
        _items.Add(new ScreenMarker(_settings.NextMarkerNumber, location));
        _settings.NextMarkerNumber = Math.Min(9999, _settings.NextMarkerNumber + 1);
        SyncSettings();
        SettingsChanged?.Invoke();
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
        EditArrowMemo(arrow, removeOnCancel: true);
    }

    private void EditArrowMemo(AnnotationArrow arrow, bool removeOnCancel)
    {
        using var form = new ArrowMemoForm(arrow.Text);
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

    private AnnotationArrow? FindMemoAt(Point location)
    {
        return _items
            .OfType<AnnotationArrow>()
            .LastOrDefault(arrow => AnnotationGeometry.GetMemoBounds(arrow, _virtualBounds).Contains(location));
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
            else if (item is AnnotationArrow arrow)
            {
                DrawArrow(graphics, arrow);
            }
        }
    }

    private void DrawMarker(Graphics graphics, ScreenMarker marker)
    {
        var center = ToLocal(marker.Location);
        var diameter = _settings.MarkerSize;
        var circle = new Rectangle(center.X - diameter / 2, center.Y - diameter / 2, diameter, diameter);
        using var brush = new SolidBrush(_settings.MarkerColor);
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

    private void DrawArrow(Graphics graphics, AnnotationArrow arrow)
    {
        var start = ToLocal(arrow.Start);
        var end = ToLocal(arrow.End);
        using var cap = new AdjustableArrowCap(5F, 6F, true);
        using var pen = new Pen(arrow.Color, arrow.Width) { CustomEndCap = cap, StartCap = LineCap.Round };
        graphics.DrawLine(pen, start, end);

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
}
