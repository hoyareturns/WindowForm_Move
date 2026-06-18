namespace WindowForm_Move;

public sealed class WindowMoveApplicationContext : ApplicationContext
{
    private readonly Dictionary<IntPtr, OverlayForm> _overlays = new();
    private readonly System.Windows.Forms.Timer _scanTimer = new();
    private readonly CrosshairOverlayForm _crosshairOverlay = new();
    private readonly NotifyIcon _notifyIcon;
    private ToolStripMenuItem? _crosshairMenuItem;
    private bool _buttonsVisible = true;
    private bool _moveAllWindows;
    private bool _crosshairEnabled;

    public WindowMoveApplicationContext()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Window Move",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => ToggleVisible();

        _scanTimer.Interval = 250;
        _scanTimer.Tick += (_, _) => SyncOverlays();
        _scanTimer.Start();
        SyncOverlays();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _scanTimer.Dispose();
            _crosshairOverlay.Dispose();
            foreach (var overlay in _overlays.Values)
            {
                overlay.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Show / Hide buttons");
        showItem.Click += (_, _) => ToggleVisible();
        menu.Items.Add(showItem);

        var allItem = new ToolStripMenuItem("Move all windows");
        allItem.CheckOnClick = true;
        allItem.CheckedChanged += (_, _) => SetMoveAllWindows(allItem.Checked);
        menu.Items.Add(allItem);

        _crosshairMenuItem = new ToolStripMenuItem("Crosshair guide")
        {
            CheckOnClick = true
        };
        _crosshairMenuItem.CheckedChanged += (_, _) => SetCrosshairEnabled(_crosshairMenuItem.Checked);
        menu.Items.Add(_crosshairMenuItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void SyncOverlays()
    {
        var windows = WindowController.GetMovableWindows();
        var liveHandles = windows.Select(window => window.Handle).ToHashSet();

        foreach (var window in windows)
        {
            if (!_overlays.ContainsKey(window.Handle))
            {
                _overlays[window.Handle] = new OverlayForm(
                    window.Handle,
                    () => _moveAllWindows,
                    () => _crosshairEnabled,
                    ToggleCrosshair,
                    ExitThread);
            }
        }

        foreach (var handle in _overlays.Keys.ToList())
        {
            if (!liveHandles.Contains(handle))
            {
                _overlays[handle].Dispose();
                _overlays.Remove(handle);
            }
        }

        foreach (var overlay in _overlays.Values)
        {
            overlay.UpdatePosition(_buttonsVisible);
        }
    }

    private void ToggleVisible()
    {
        _buttonsVisible = !_buttonsVisible;
        SyncOverlays();
    }

    private void SetMoveAllWindows(bool enabled)
    {
        _moveAllWindows = enabled;
    }

    private void ToggleCrosshair()
    {
        SetCrosshairEnabled(!_crosshairEnabled);
    }

    private void SetCrosshairEnabled(bool enabled)
    {
        _crosshairEnabled = enabled;
        _crosshairOverlay.SetEnabled(enabled);

        if (_crosshairMenuItem is not null && _crosshairMenuItem.Checked != enabled)
        {
            _crosshairMenuItem.Checked = enabled;
        }

        foreach (var overlay in _overlays.Values)
        {
            overlay.SyncCrosshairState();
        }
    }
}
