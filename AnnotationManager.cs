using System.Drawing.Imaging;

namespace WindowForm_Move;

public sealed class AnnotationManager : IDisposable
{
    private readonly List<AnnotationItem> _items = new();
    private AnnotationOverlayForm? _overlay;
    private readonly AnnotationSettings _settings = AnnotationSettings.Load();
    private readonly GlobalMouseHook _mouseHook = new();
    private AnnotationArrow? _pendingArrow;
    private AnnotationArrow? _queuedMemoArrow;
    private bool _removeQueuedArrowOnCancel;
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
        if (tool != AnnotationTool.Arrow)
        {
            CancelPendingArrow();
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
        if (ReferenceEquals(removed, _pendingArrow))
        {
            _pendingArrow = null;
        }
        RefreshOverlays();
    }

    public void ClearAll()
    {
        _items.Clear();
        _pendingArrow = null;
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
        finally
        {
            RefreshOverlays();
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

    private void AddArrowPoint(Point location)
    {
        if (_pendingArrow is null)
        {
            _pendingArrow = new AnnotationArrow(location, location, _settings.ArrowColor, _settings.ArrowWidth);
            _items.Add(_pendingArrow);
            RefreshArea(location, location, _settings.ArrowWidth + 8F);
            return;
        }

        var arrow = _pendingArrow;
        _pendingArrow = null;
        arrow.End = location;
        arrow.IsPending = false;
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
                _dragging = true;
                if (ActiveTool == AnnotationTool.Marker)
                {
                    AddMarker(mouseEvent.Location);
                    _dragging = false;
                }
                else if (ActiveTool == AnnotationTool.Arrow)
                {
                    if (_pendingArrow is null && TryScheduleMemoEdit(mouseEvent.Location))
                    {
                        _dragging = false;
                        return true;
                    }

                    AddArrowPoint(mouseEvent.Location);
                    _dragging = false;
                }
                else if (ActiveTool == AnnotationTool.Eraser)
                {
                    EraseAt(mouseEvent.Location);
                }
                return true;

            case GlobalMouseEventType.Move when _dragging:
                if (ActiveTool == AnnotationTool.Eraser)
                {
                    EraseAt(mouseEvent.Location);
                }
                return true;

            case GlobalMouseEventType.LeftUp:
                _dragging = false;
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
        if (!arrow.IsPending && memoBounds.Contains(location))
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
            return arrow.IsPending
                ? arrowBounds
                : Rectangle.Union(arrowBounds, AnnotationGeometry.GetMemoBounds(arrow, SystemInformation.VirtualScreen));
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
            .LastOrDefault(item => !item.IsPending &&
                AnnotationGeometry.GetMemoBounds(item, SystemInformation.VirtualScreen).Contains(location));
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

    private void CancelPendingArrow()
    {
        if (_pendingArrow is null)
        {
            return;
        }

        _items.Remove(_pendingArrow);
        _pendingArrow = null;
        RefreshOverlays();
    }
}
