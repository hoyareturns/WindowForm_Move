using System.Drawing.Imaging;

namespace WindowForm_Move;

public sealed class AnnotationManager : IDisposable
{
    private readonly List<AnnotationItem> _items = new();
    private AnnotationOverlayForm? _overlay;
    private readonly AnnotationSettings _settings = AnnotationSettings.Load();
    private readonly GlobalMouseHook _mouseHook = new();
    private Point? _arrowStart;
    private Point _arrowPreviewEnd;
    private bool _arrowPreviewVisible;
    private AnnotationArrow? _queuedMemoArrow;
    private bool _removeQueuedArrowOnCancel;
    private bool _dragging;

    public AnnotationManager()
    {
        _settings.NextMarkerNumber = 1;
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
        if (tool != AnnotationTool.Arrow)
        {
            CancelArrowDrag();
        }

        if (tool == AnnotationTool.None)
        {
            _dragging = false;
            _queuedMemoArrow = null;
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

        var removed = _items[^1];
        _items.RemoveAt(_items.Count - 1);
        RefreshOverlays();
    }

    public void ClearAll()
    {
        _items.Clear();
        CancelArrowDrag();
        RefreshOverlays();
    }

    public void CaptureSelectedRegion()
    {
        SetActiveTool(AnnotationTool.None);
        Application.DoEvents();

        var virtualBounds = SystemInformation.VirtualScreen;
        Bitmap source;
        try
        {
            source = ScreenCapture.Capture(virtualBounds);
        }
        catch (Exception exception)
        {
            _overlay?.Hide();
            MessageBox.Show(exception.Message, "캡처 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RefreshOverlays();
            return;
        }

        using (source)
        {
            _overlay?.Hide();
            try
            {
                using var selectionImage = (Bitmap)source.Clone();
                using var selector = new CaptureSelectionForm(selectionImage, virtualBounds);
                if (selector.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                var selected = selector.SelectedRegion;
                using var result = source.Clone(selected, PixelFormat.Format32bppArgb);
                var outputPath = CreateCapturePath();
                result.Save(outputPath, ImageFormat.Png);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "캡처 저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                RefreshOverlays();
            }
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
        RefreshArea(location, location, _settings.MarkerSize / 2F + 3F);
    }

    public void ShowSettings()
    {
        var previousTool = ActiveTool;
        SetActiveTool(AnnotationTool.None);
        _overlay?.Hide();
        try
        {
            using var form = new AnnotationSettingsForm(_settings);
            if (form.ShowDialog() == DialogResult.OK)
            {
                form.ApplyTo(_settings);
                _settings.Save();
            }
        }
        finally
        {
            RefreshOverlays();
            SetActiveTool(previousTool);
        }
    }

    private IReadOnlyList<AnnotationItem> GetItems()
    {
        return _items;
    }

    private AnnotationSettings GetSettings()
    {
        return _settings;
    }

    private void BeginArrowDrag(Point location)
    {
        CancelArrowDrag();
        _arrowStart = location;
        _arrowPreviewEnd = location;
        _dragging = true;
    }

    private void UpdateArrowPreview(Point location)
    {
        if (_arrowStart is null)
        {
            return;
        }

        EraseArrowPreview();
        _arrowPreviewEnd = location;
        if (_arrowStart.Value != location)
        {
            ControlPaint.DrawReversibleLine(_arrowStart.Value, location, _settings.ArrowColor);
            _arrowPreviewVisible = true;
        }
    }

    private void CompleteArrowDrag(Point location)
    {
        if (_arrowStart is null)
        {
            return;
        }

        var start = _arrowStart.Value;
        EraseArrowPreview();
        _arrowStart = null;
        _dragging = false;
        if (DistanceSquared(start, location) < 25F)
        {
            return;
        }

        var arrow = new AnnotationArrow(start, location, _settings.ArrowColor, _settings.ArrowWidth);
        _items.Add(arrow);
        RefreshOverlays();

        QueueMemoEdit(arrow, removeOnCancel: true);
    }

    private void EnsureOverlay()
    {
        var virtualBounds = SystemInformation.VirtualScreen;
        if (_overlay is null || _overlay.IsDisposed)
        {
            _overlay = new AnnotationOverlayForm(
                virtualBounds,
                GetItems,
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
                if (ActiveTool == AnnotationTool.Marker)
                {
                    AddMarker(mouseEvent.Location);
                    _dragging = false;
                }
                else if (ActiveTool == AnnotationTool.Arrow)
                {
                    if (TryScheduleMemoEdit(mouseEvent.Location))
                    {
                        _dragging = false;
                        return true;
                    }

                    BeginArrowDrag(mouseEvent.Location);
                }
                else if (ActiveTool == AnnotationTool.Eraser)
                {
                    _dragging = true;
                    EraseAt(mouseEvent.Location);
                }
                return true;

            case GlobalMouseEventType.Move when _dragging:
                if (ActiveTool == AnnotationTool.Arrow)
                {
                    UpdateArrowPreview(mouseEvent.Location);
                }
                else if (ActiveTool == AnnotationTool.Eraser)
                {
                    EraseAt(mouseEvent.Location);
                }
                return true;

            case GlobalMouseEventType.LeftUp:
                if (_dragging && ActiveTool == AnnotationTool.Arrow)
                {
                    CompleteArrowDrag(mouseEvent.Location);
                }
                else
                {
                    _dragging = false;
                }
                OpenQueuedMemoAfterMouseUp();
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

        if (item is not AnnotationArrow arrow)
        {
            return false;
        }

        var memoBounds = AnnotationGeometry.GetMemoBounds(arrow, SystemInformation.VirtualScreen);
        if (memoBounds.Contains(location))
        {
            return true;
        }

        return DistanceToSegment(location, arrow.Start, arrow.End) <= arrow.Width / 2F + 8F;
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

        if (item is AnnotationArrow arrow)
        {
            var pad = (int)Math.Ceiling(arrow.Width + 8F);
            var arrowBounds = Rectangle.FromLTRB(
                Math.Min(arrow.Start.X, arrow.End.X) - pad,
                Math.Min(arrow.Start.Y, arrow.End.Y) - pad,
                Math.Max(arrow.Start.X, arrow.End.X) + pad + 1,
                Math.Max(arrow.Start.Y, arrow.End.Y) + pad + 1);
            return Rectangle.Union(arrowBounds, AnnotationGeometry.GetMemoBounds(arrow, SystemInformation.VirtualScreen));
        }

        return Rectangle.Empty;
    }

    private void EditArrowMemo(AnnotationArrow arrow, bool removeOnCancel)
    {
        var previousTool = ActiveTool;
        SetActiveTool(AnnotationTool.None);
        _overlay?.Hide();
        try
        {
            using var form = new ArrowMemoForm(arrow.Text);
            if (form.ShowDialog() == DialogResult.OK)
            {
                arrow.Text = form.MemoText;
            }
            else if (removeOnCancel)
            {
                _items.Remove(arrow);
            }
        }
        finally
        {
            RefreshOverlays();
            SetActiveTool(previousTool);
        }
    }

    private bool TryScheduleMemoEdit(Point location)
    {
        var arrow = _items
            .OfType<AnnotationArrow>()
            .LastOrDefault(item => AnnotationGeometry.GetMemoBounds(item, SystemInformation.VirtualScreen).Contains(location));
        if (arrow is null || _overlay is null || _overlay.IsDisposed)
        {
            return false;
        }

        QueueMemoEdit(arrow, removeOnCancel: false);
        return true;
    }

    private void QueueMemoEdit(AnnotationArrow arrow, bool removeOnCancel)
    {
        _queuedMemoArrow = arrow;
        _removeQueuedArrowOnCancel = removeOnCancel;
    }

    private void OpenQueuedMemoAfterMouseUp()
    {
        if (_queuedMemoArrow is null || _overlay is null || _overlay.IsDisposed)
        {
            return;
        }

        var arrow = _queuedMemoArrow;
        var removeOnCancel = _removeQueuedArrowOnCancel;
        _queuedMemoArrow = null;
        _overlay.BeginInvoke(new Action(() => EditArrowMemo(arrow, removeOnCancel)));
    }

    private void CancelArrowDrag()
    {
        EraseArrowPreview();
        _arrowStart = null;
        _dragging = false;
    }

    private void EraseArrowPreview()
    {
        if (!_arrowPreviewVisible || _arrowStart is null)
        {
            return;
        }

        ControlPaint.DrawReversibleLine(_arrowStart.Value, _arrowPreviewEnd, _settings.ArrowColor);
        _arrowPreviewVisible = false;
    }

    private string CreateCapturePath()
    {
        var directory = string.IsNullOrWhiteSpace(_settings.CaptureDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            : Environment.ExpandEnvironmentVariables(_settings.CaptureDirectory.Trim());
        Directory.CreateDirectory(directory);

        var now = DateTime.Now;
        var pattern = string.IsNullOrWhiteSpace(_settings.CaptureFileNamePattern)
            ? "{date}_{time}"
            : _settings.CaptureFileNamePattern.Trim();
        var fileName = pattern
            .Replace("{datetime}", now.ToString("yyyyMMdd_HHmmss"), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", now.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", now.ToString("HHmmss"), StringComparison.OrdinalIgnoreCase);

        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidCharacter, '_');
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = now.ToString("yyyyMMdd_HHmmss");
        }

        if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^4];
        }

        var path = Path.Combine(directory, fileName + ".png");
        var suffix = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(directory, $"{fileName}_{suffix++}.png");
        }

        return path;
    }
}
