using System.Drawing.Imaging;

namespace WindowForm_Move;

public sealed class AnnotationManager : IDisposable
{
    private readonly List<AnnotationItem> _items = new();
    private readonly Dictionary<string, AnnotationOverlayForm> _overlays = new(StringComparer.OrdinalIgnoreCase);
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
        EnsureOverlays();
        foreach (var overlay in _overlays.Values)
        {
            overlay.SetActiveTool(tool);
        }
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
        foreach (var overlay in _overlays.Values)
        {
            overlay.Dispose();
        }

        _overlays.Clear();
    }

    private void AddMarker(Point location)
    {
        var markerNumber = _items.OfType<ScreenMarker>().Select(marker => marker.Number).DefaultIfEmpty(0).Max() + 1;
        _items.Add(new ScreenMarker(markerNumber, location));
        RefreshOverlays();
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
        RefreshOverlays();
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
        RefreshOverlays();
    }

    private void EndStroke()
    {
        _activeStroke = null;
    }

    private void EnsureOverlays()
    {
        var screens = Screen.AllScreens;
        var liveNames = screens.Select(screen => screen.DeviceName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var screen in screens)
        {
            if (!_overlays.ContainsKey(screen.DeviceName))
            {
                _overlays[screen.DeviceName] = new AnnotationOverlayForm(
                    screen,
                    GetItems,
                    AddMarker,
                    BeginStroke,
                    AppendStroke,
                    EndStroke,
                    EraseAt,
                    GetSettings);
            }
        }

        foreach (var name in _overlays.Keys.Where(name => !liveNames.Contains(name)).ToList())
        {
            _overlays[name].Dispose();
            _overlays.Remove(name);
        }
    }

    private void RefreshOverlays()
    {
        EnsureOverlays();
        foreach (var overlay in _overlays.Values)
        {
            overlay.RefreshAnnotations();
        }
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
            RefreshOverlays();
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
}
