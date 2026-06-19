using System.Drawing.Imaging;

namespace WindowForm_Move;

public sealed class AnnotationManager : IDisposable
{
    private readonly List<AnnotationItem> _items = new();
    private AnnotationOverlayForm? _overlay;
    private readonly AnnotationSettings _settings = AnnotationSettings.Load();
    private readonly GlobalMouseHook _mouseHook = new();
    private DrawingStroke? _activeStroke;
    private bool _dragging;

    public AnnotationManager()
    {
        _mouseHook.Handler = HandleGlobalMouse;
    }

    public AnnotationTool ActiveTool { get; private set; }

    public void ToggleTool(AnnotationTool tool)
    {
        SetActiveTool(ActiveTool == tool ? AnnotationTool.None : tool);
    }

    public void SetActiveTool(AnnotationTool tool)
    {
        ActiveTool = tool;
        if (tool == AnnotationTool.None)
        {
            _dragging = false;
            _activeStroke = null;
            _mouseHook.Stop();
        }
        else
        {
            _mouseHook.Start();
        }
        EnsureOverlay();
        _overlay?.SetActiveTool(tool);
    }

    public void UndoLast()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _items.RemoveAt(_items.Count - 1);
        RefreshOverlays();
    }

    public void ClearAll()
    {
        _items.Clear();
        RefreshOverlays();
    }

    public void CaptureSelectedRegion()
    {
        SetActiveTool(AnnotationTool.None);
        Application.DoEvents();

        var virtualBounds = SystemInformation.VirtualScreen;
        using var source = new Bitmap(virtualBounds.Width, virtualBounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.CopyFromScreen(
                virtualBounds.Location,
                Point.Empty,
                virtualBounds.Size,
                CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt);
        }

        using var selectionImage = (Bitmap)source.Clone();
        using var selector = new CaptureSelectionForm(selectionImage, virtualBounds);
        if (selector.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var selected = selector.SelectedRegion;
        using var result = source.Clone(selected, PixelFormat.Format32bppArgb);
        using var saveDialog = new SaveFileDialog
        {
            Title = "마킹 화면 저장",
            Filter = "PNG 이미지 (*.png)|*.png",
            DefaultExt = "png",
            AddExtension = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            FileName = $"WindowForm_Move_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };

        if (saveDialog.ShowDialog() == DialogResult.OK)
        {
            result.Save(saveDialog.FileName, ImageFormat.Png);
        }
    }

    public void Dispose()
    {
        _mouseHook.Dispose();
        _overlay?.Dispose();
        _overlay = null;
    }

    private void AddMarker(Point location)
    {
        var markerNumber = _settings.NextMarkerNumber;
        _items.Add(new ScreenMarker(markerNumber, location));
        _settings.NextMarkerNumber++;
        _settings.Save();
        RefreshArea(location, location, _settings.MarkerSize / 2F + 3F);
    }

    public void ShowSettings()
    {
        var previousTool = ActiveTool;
        SetActiveTool(AnnotationTool.None);
        using var form = new AnnotationSettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            form.ApplyTo(_settings);
            _settings.Save();
            RefreshOverlays();
        }

        SetActiveTool(previousTool);
    }

    private IReadOnlyList<AnnotationItem> GetItems()
    {
        return _items;
    }

    private AnnotationSettings GetSettings()
    {
        return _settings;
    }

    private void BeginStroke(Point location)
    {
        _activeStroke = new DrawingStroke(new List<Point> { location }, _settings.PenColor, _settings.PenWidth);
        _items.Add(_activeStroke);
        RefreshArea(location, location, _settings.PenWidth + 3F);
    }

    private void AppendStroke(Point location)
    {
        if (_activeStroke is null)
        {
            return;
        }

        var last = _activeStroke.Points[^1];
        if (Math.Abs(last.X - location.X) + Math.Abs(last.Y - location.Y) < 2)
        {
            return;
        }

        _activeStroke.Points.Add(location);
        RefreshArea(last, location, _activeStroke.Width + 3F);
    }

    private void EndStroke()
    {
        _activeStroke = null;
    }

    private void EnsureOverlay()
    {
        var virtualBounds = SystemInformation.VirtualScreen;
        if (_overlay is null || _overlay.IsDisposed)
        {
            _overlay = new AnnotationOverlayForm(
                virtualBounds,
                GetItems,
                AddMarker,
                BeginStroke,
                AppendStroke,
                EndStroke,
                EraseAt,
                GetSettings);
        }
        else
        {
            _overlay.UpdateVirtualBounds(virtualBounds);
        }
    }

    private void RefreshOverlays()
    {
        EnsureOverlay();
        _overlay?.RefreshAnnotations();
    }

    private void EraseAt(Point location)
    {
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            if (!HitTest(_items[index], location))
            {
                continue;
            }

            var invalidArea = GetItemBounds(_items[index]);
            _items.RemoveAt(index);
            EnsureOverlay();
            _overlay?.RefreshAnnotations(invalidArea);
            return;
        }
    }

    private bool HandleGlobalMouse(GlobalMouseEvent mouseEvent)
    {
        if (ActiveTool == AnnotationTool.None || (!_dragging && IsOverApplicationControl(mouseEvent.Location)))
        {
            return false;
        }

        switch (mouseEvent.Type)
        {
            case GlobalMouseEventType.LeftDown:
                _dragging = true;
                if (ActiveTool == AnnotationTool.Marker)
                {
                    AddMarker(mouseEvent.Location);
                    _dragging = false;
                }
                else if (ActiveTool == AnnotationTool.Pen)
                {
                    BeginStroke(mouseEvent.Location);
                }
                else if (ActiveTool == AnnotationTool.Eraser)
                {
                    EraseAt(mouseEvent.Location);
                }
                return true;

            case GlobalMouseEventType.Move when _dragging:
                if (ActiveTool == AnnotationTool.Pen)
                {
                    AppendStroke(mouseEvent.Location);
                }
                else if (ActiveTool == AnnotationTool.Eraser)
                {
                    EraseAt(mouseEvent.Location);
                }
                return true;

            case GlobalMouseEventType.LeftUp:
                if (_dragging && ActiveTool == AnnotationTool.Pen)
                {
                    EndStroke();
                }
                _dragging = false;
                return true;
        }

        return false;
    }

    private static bool IsOverApplicationControl(Point location)
    {
        return Application.OpenForms
            .Cast<Form>()
            .Any(form => form is not AnnotationOverlayForm && form.Visible && form.Bounds.Contains(location));
    }

    private bool HitTest(AnnotationItem item, Point location)
    {
        if (item is ScreenMarker marker)
        {
            var radius = _settings.MarkerSize / 2F + 8F;
            return DistanceSquared(marker.Location, location) <= radius * radius;
        }

        if (item is not DrawingStroke stroke || stroke.Points.Count == 0)
        {
            return false;
        }

        var tolerance = stroke.Width / 2F + 8F;
        if (stroke.Points.Count == 1)
        {
            return DistanceSquared(stroke.Points[0], location) <= tolerance * tolerance;
        }

        for (var index = 1; index < stroke.Points.Count; index++)
        {
            if (DistanceToSegment(location, stroke.Points[index - 1], stroke.Points[index]) <= tolerance)
            {
                return true;
            }
        }

        return false;
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

    private void RefreshArea(Point first, Point second, float padding)
    {
        var pad = (int)Math.Ceiling(padding);
        var area = Rectangle.FromLTRB(
            Math.Min(first.X, second.X) - pad,
            Math.Min(first.Y, second.Y) - pad,
            Math.Max(first.X, second.X) + pad + 1,
            Math.Max(first.Y, second.Y) + pad + 1);
        EnsureOverlay();
        _overlay?.RefreshAnnotations(area);
    }

    private Rectangle GetItemBounds(AnnotationItem item)
    {
        if (item is ScreenMarker marker)
        {
            var radius = _settings.MarkerSize / 2 + 3;
            return new Rectangle(marker.Location.X - radius, marker.Location.Y - radius, radius * 2 + 1, radius * 2 + 1);
        }

        if (item is DrawingStroke stroke && stroke.Points.Count > 0)
        {
            var pad = (int)Math.Ceiling(stroke.Width + 3F);
            return Rectangle.FromLTRB(
                stroke.Points.Min(point => point.X) - pad,
                stroke.Points.Min(point => point.Y) - pad,
                stroke.Points.Max(point => point.X) + pad + 1,
                stroke.Points.Max(point => point.Y) + pad + 1);
        }

        return Rectangle.Empty;
    }
}
