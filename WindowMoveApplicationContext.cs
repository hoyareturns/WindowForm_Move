namespace WindowForm_Move;

public sealed class WindowMoveApplicationContext : ApplicationContext
{
    private readonly Dictionary<IntPtr, OverlayForm> _overlays = new();
    private readonly System.Windows.Forms.Timer _scanTimer = new();
    private readonly CrosshairOverlayForm _crosshairOverlay = new();
    private readonly AnnotationManager _annotationManager = new();
    private readonly WindowLayoutStore _layoutStore = new();
    private readonly ProgramLaunchStore _programStore = new();
    private readonly NotifyIcon _notifyIcon;
    private ToolStripMenuItem? _moveAllMenuItem;
    private ToolStripMenuItem? _crosshairMenuItem;
    private bool _buttonsVisible = true;
    private bool _moveAllWindows;
    private bool _crosshairEnabled;
    private bool _layoutControlsExpanded;
    private bool _annotationControlsExpanded;
    private bool _programControlsExpanded;
    private bool _presentationActive;
    private IntPtr _presentationHostHandle;

    public WindowMoveApplicationContext()
    {
        _annotationManager.ToolbarStateChanged += SyncOverlayToggleStates;
        _annotationManager.PresentationStarted += BeginPresentation;
        _annotationManager.PresentationReady += ShowPresentationToolbar;
        _annotationManager.PresentationEnded += EndPresentation;
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Smart_Window",
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
            _annotationManager.Dispose();
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

        _moveAllMenuItem = new ToolStripMenuItem("Move all windows")
        {
            CheckOnClick = true
        };
        _moveAllMenuItem.CheckedChanged += (_, _) => SetMoveAllWindows(_moveAllMenuItem.Checked);
        menu.Items.Add(_moveAllMenuItem);

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
        if (_presentationActive)
        {
            foreach (var pair in _overlays)
            {
                if (pair.Key != _presentationHostHandle)
                {
                    pair.Value.Hide();
                }
            }

            return;
        }

        var windows = WindowController.GetMovableWindows();
        var liveHandles = windows.Select(window => window.Handle).ToHashSet();

        foreach (var window in windows)
        {
            if (!_overlays.ContainsKey(window.Handle))
            {
                _overlays[window.Handle] = new OverlayForm(
                    window.Handle,
                    () => _moveAllWindows,
                    ToggleMoveAllWindows,
                    () => _crosshairEnabled,
                    ToggleCrosshair,
                    _layoutStore.GetNames,
                    SaveLayout,
                    LoadLayout,
                    DeleteLayout,
                    () => _layoutControlsExpanded,
                    ToggleLayoutControls,
                    _programStore.GetNames,
                    name => SaveProgram(window.Handle, name),
                    LoadProgram,
                    name => EditProgram(window.Handle, name),
                    DeleteProgram,
                    () => _programControlsExpanded,
                    ToggleProgramControls,
                    () => _annotationManager.ActiveTool,
                    tool => ToggleAnnotationTool(window.Handle, tool),
                    () => _annotationManager.MarkerColor,
                    _annotationManager.ChooseMarkerColor,
                    () => _annotationManager.PenColor,
                    _annotationManager.ChoosePenColor,
                    () => _annotationManager.NextMarkerNumber,
                    _annotationManager.SetNextMarkerNumber,
                    _annotationManager.UndoLast,
                    _annotationManager.ClearAll,
                    () => CaptureSelectedRegion(window.Handle),
                    _annotationManager.ShowSettings,
                    () => _annotationControlsExpanded,
                    ToggleAnnotationControls,
                    () => _annotationManager.ShowAnnotationSet,
                    () => _annotationManager.ShowLayoutSet,
                    () => _annotationManager.ShowProgramSet,
                    () => _annotationManager.ToolbarColor,
                    () => _annotationManager.MatchTargetWindowColor,
                    () => _annotationManager.SharpIconRendering,
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

    private void BeginPresentation()
    {
        _presentationActive = true;
        foreach (var overlay in _overlays.Values)
        {
            overlay.Hide();
        }
    }

    private void EndPresentation()
    {
        foreach (var overlay in _overlays.Values)
        {
            overlay.EndPresentationMode();
        }

        _presentationActive = false;
        _presentationHostHandle = IntPtr.Zero;
        SyncOverlays();
    }

    private void ShowPresentationToolbar()
    {
        if (!_overlays.TryGetValue(_presentationHostHandle, out var host))
        {
            return;
        }

        host.SyncToggleStates();
        _annotationManager.AttachPresentationToolbar(host);
        host.ShowForPresentation();
        _annotationManager.PositionPresentationNotice(host.Bounds);
    }

    private void SetMoveAllWindows(bool enabled)
    {
        _moveAllWindows = enabled;

        if (_moveAllMenuItem is not null && _moveAllMenuItem.Checked != enabled)
        {
            _moveAllMenuItem.Checked = enabled;
        }

        SyncOverlayToggleStates();
    }

    private void ToggleMoveAllWindows()
    {
        SetMoveAllWindows(!_moveAllWindows);
    }

    private void ToggleCrosshair()
    {
        SetCrosshairEnabled(!_crosshairEnabled);
    }

    private void ToggleLayoutControls()
    {
        _layoutControlsExpanded = !_layoutControlsExpanded;
        SyncOverlayToggleStates();
        SyncOverlays();
    }

    private void ToggleAnnotationControls()
    {
        _annotationControlsExpanded = !_annotationControlsExpanded;
        SyncOverlayToggleStates();
        SyncOverlays();
    }

    private void ToggleProgramControls()
    {
        _programControlsExpanded = !_programControlsExpanded;
        SyncOverlayToggleStates();
        SyncOverlays();
    }

    private void ToggleAnnotationTool(IntPtr hostHandle, AnnotationTool tool)
    {
        _presentationHostHandle = hostHandle;
        _annotationControlsExpanded = true;
        if (_crosshairEnabled)
        {
            SetCrosshairEnabled(false);
        }

        _annotationManager.ToggleTool(tool);
        SyncOverlayToggleStates();
    }

    private void CaptureSelectedRegion()
    {
        _annotationManager.CaptureSelectedRegion();
        SyncOverlayToggleStates();
    }

    private void CaptureSelectedRegion(IntPtr hostHandle)
    {
        _presentationHostHandle = hostHandle;
        CaptureSelectedRegion();
    }

    private void SetCrosshairEnabled(bool enabled)
    {
        _crosshairEnabled = enabled;
        _crosshairOverlay.SetEnabled(enabled);

        if (_crosshairMenuItem is not null && _crosshairMenuItem.Checked != enabled)
        {
            _crosshairMenuItem.Checked = enabled;
        }

        SyncOverlayToggleStates();
    }

    private void SyncOverlayToggleStates()
    {
        foreach (var overlay in _overlays.Values)
        {
            overlay.SyncToggleStates();
        }
    }

    private bool SaveLayout(string name)
    {
        name = NormalizeLayoutName(name);
        var succeeded = _layoutStore.Save(name);
        if (succeeded)
        {
            RefreshLayoutControls(name);
        }

        return succeeded;
    }

    private bool LoadLayout(string name)
    {
        var succeeded = _layoutStore.Load(name);
        if (succeeded)
        {
            RefreshLayoutControls(name.Trim());
        }

        return succeeded;
    }

    private bool DeleteLayout(string name)
    {
        var succeeded = _layoutStore.Delete(name);
        if (succeeded)
        {
            RefreshLayoutControls(string.Empty);
        }

        return succeeded;
    }

    private void RefreshLayoutControls(string selectedName)
    {
        foreach (var overlay in _overlays.Values)
        {
            overlay.RefreshLayoutNames(selectedName);
        }
    }

    private string? SaveProgram(IntPtr hostHandle, string preferredName)
    {
        _overlays.TryGetValue(hostHandle, out var owner);
        var savedName = _programStore.Save(preferredName, owner);
        if (savedName is not null)
        {
            RefreshProgramControls(savedName);
        }

        return savedName;
    }

    private bool LoadProgram(string name)
    {
        var succeeded = _programStore.Load(name, out var error);
        if (!succeeded && !string.IsNullOrWhiteSpace(error))
        {
            MessageBox.Show(error, "프로그램 실행 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        return succeeded;
    }

    private bool DeleteProgram(string name)
    {
        var succeeded = _programStore.Delete(name);
        if (succeeded)
        {
            RefreshProgramControls(string.Empty);
        }

        return succeeded;
    }

    private string? EditProgram(IntPtr hostHandle, string name)
    {
        _overlays.TryGetValue(hostHandle, out var owner);
        var editedName = _programStore.Edit(name, owner);
        if (editedName is not null)
        {
            RefreshProgramControls(editedName);
        }

        return editedName;
    }

    private void RefreshProgramControls(string selectedName)
    {
        foreach (var overlay in _overlays.Values)
        {
            overlay.RefreshProgramNames(selectedName);
        }
    }

    private string NormalizeLayoutName(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        var number = 1;
        var names = _layoutStore.GetNames();
        while (names.Contains($"Layout {number}", StringComparer.CurrentCultureIgnoreCase))
        {
            number++;
        }

        return $"Layout {number}";
    }
}
