namespace WindowForm_Move;

public sealed class OverlayForm : Form
{
    private const int InitialToolbarWidth = 468;
    private const int ToolbarEdgeInset = 0;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;
    private readonly Func<bool> _getMoveAllWindows;
    private readonly Action _toggleMoveAllWindows;
    private readonly Func<bool> _getCrosshairEnabled;
    private readonly Action _toggleCrosshair;
    private readonly Action<Rectangle> _showAnnotationTools;
    private readonly Func<IReadOnlyList<string>> _getLayoutNames;
    private readonly Func<string, bool> _saveLayout;
    private readonly Func<string, bool> _loadLayout;
    private readonly Func<string, bool> _deleteLayout;
    private readonly Func<bool> _getLayoutControlsExpanded;
    private readonly Action _toggleLayoutControls;
    private readonly Func<IReadOnlyList<string>> _getProgramNames;
    private readonly Func<string, string?> _saveProgram;
    private readonly Func<string, bool> _loadProgram;
    private readonly Func<string, string?> _editProgram;
    private readonly Func<string, bool> _deleteProgram;
    private readonly Func<bool> _getProgramControlsExpanded;
    private readonly Action _toggleProgramControls;
    private readonly Func<AnnotationTool> _getAnnotationTool;
    private readonly Action<AnnotationTool> _toggleAnnotationTool;
    private readonly Func<Color> _getMarkerColor;
    private readonly Action _chooseMarkerColor;
    private readonly Func<Color> _getPenColor;
    private readonly Action _choosePenColor;
    private readonly Func<int> _getNextMarkerNumber;
    private readonly Action<int> _setNextMarkerNumber;
    private readonly Action _undoAnnotation;
    private readonly Action _clearAnnotations;
    private readonly Action _captureSelectedRegion;
    private readonly Action _showAnnotationSettings;
    private readonly Func<bool> _getAnnotationControlsExpanded;
    private readonly Action _toggleAnnotationControls;
    private readonly Func<bool> _getShowAnnotationSet;
    private readonly Func<bool> _getShowLayoutSet;
    private readonly Func<bool> _getShowProgramSet;
    private readonly Func<Color> _getToolbarColor;
    private readonly Func<bool> _getMatchTargetWindowColor;
    private readonly Func<bool> _getSharpIconRendering;
    private readonly Func<string, ButtonPreference> _getButtonPreference;
    private readonly Func<string, string> _getButtonName;
    private readonly Action _exitRequested;
    private readonly ComboBox _layoutCombo = new();
    private readonly ComboBox _programCombo = new();
    private readonly ToolTip _toolTip = new();
    private readonly List<Control> _layoutControls = new();
    private readonly List<Control> _programControls = new();
    private FlowLayoutPanel? _toolbarPanel;
    private bool _toolbarMinimized;
    private Color? _sampledTargetColor;
    private DateTime _nextTargetColorSampleUtc;
    private Button? _allButton;
    private Button? _crosshairButton;
    private Button? _annotationToolsButton;
    private Button? _layoutLeftToggleButton;
    private Button? _layoutRightToggleButton;
    private Button? _programLeftToggleButton;
    private Button? _programRightToggleButton;
    private Button? _toolbarMinimizeButton;
    private readonly Dictionary<string, List<Control>> _configuredControls = new();
    private readonly Dictionary<string, Action> _buttonActions = new();

    public IntPtr TargetWindow { get; }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public OverlayForm(
        IntPtr targetWindow,
        Func<bool> getMoveAllWindows,
        Action toggleMoveAllWindows,
        Func<bool> getCrosshairEnabled,
        Action toggleCrosshair,
        Action<Rectangle> showAnnotationTools,
        Func<IReadOnlyList<string>> getLayoutNames,
        Func<string, bool> saveLayout,
        Func<string, bool> loadLayout,
        Func<string, bool> deleteLayout,
        Func<bool> getLayoutControlsExpanded,
        Action toggleLayoutControls,
        Func<IReadOnlyList<string>> getProgramNames,
        Func<string, string?> saveProgram,
        Func<string, bool> loadProgram,
        Func<string, string?> editProgram,
        Func<string, bool> deleteProgram,
        Func<bool> getProgramControlsExpanded,
        Action toggleProgramControls,
        Func<AnnotationTool> getAnnotationTool,
        Action<AnnotationTool> toggleAnnotationTool,
        Func<Color> getMarkerColor,
        Action chooseMarkerColor,
        Func<Color> getPenColor,
        Action choosePenColor,
        Func<int> getNextMarkerNumber,
        Action<int> setNextMarkerNumber,
        Action undoAnnotation,
        Action clearAnnotations,
        Action captureSelectedRegion,
        Action showAnnotationSettings,
        Func<bool> getAnnotationControlsExpanded,
        Action toggleAnnotationControls,
        Func<bool> getShowAnnotationSet,
        Func<bool> getShowLayoutSet,
        Func<bool> getShowProgramSet,
        Func<Color> getToolbarColor,
        Func<bool> getMatchTargetWindowColor,
        Func<bool> getSharpIconRendering,
        Func<string, ButtonPreference> getButtonPreference,
        Func<string, string> getButtonName,
        bool startExpanded,
        Action exitRequested)
    {
        TargetWindow = targetWindow;
        _getMoveAllWindows = getMoveAllWindows;
        _toggleMoveAllWindows = toggleMoveAllWindows;
        _getCrosshairEnabled = getCrosshairEnabled;
        _toggleCrosshair = toggleCrosshair;
        _showAnnotationTools = showAnnotationTools;
        _getLayoutNames = getLayoutNames;
        _saveLayout = saveLayout;
        _loadLayout = loadLayout;
        _deleteLayout = deleteLayout;
        _getLayoutControlsExpanded = getLayoutControlsExpanded;
        _toggleLayoutControls = toggleLayoutControls;
        _getProgramNames = getProgramNames;
        _saveProgram = saveProgram;
        _loadProgram = loadProgram;
        _editProgram = editProgram;
        _deleteProgram = deleteProgram;
        _getProgramControlsExpanded = getProgramControlsExpanded;
        _toggleProgramControls = toggleProgramControls;
        _getAnnotationTool = getAnnotationTool;
        _toggleAnnotationTool = toggleAnnotationTool;
        _getMarkerColor = getMarkerColor;
        _chooseMarkerColor = chooseMarkerColor;
        _getPenColor = getPenColor;
        _choosePenColor = choosePenColor;
        _getNextMarkerNumber = getNextMarkerNumber;
        _setNextMarkerNumber = setNextMarkerNumber;
        _undoAnnotation = undoAnnotation;
        _clearAnnotations = clearAnnotations;
        _captureSelectedRegion = captureSelectedRegion;
        _showAnnotationSettings = showAnnotationSettings;
        _getAnnotationControlsExpanded = getAnnotationControlsExpanded;
        _toggleAnnotationControls = toggleAnnotationControls;
        _getShowAnnotationSet = getShowAnnotationSet;
        _getShowLayoutSet = getShowLayoutSet;
        _getShowProgramSet = getShowProgramSet;
        _getToolbarColor = getToolbarColor;
        _getMatchTargetWindowColor = getMatchTargetWindowColor;
        _getSharpIconRendering = getSharpIconRendering;
        _getButtonPreference = getButtonPreference;
        _getButtonName = getButtonName;
        _toolbarMinimized = !startExpanded;
        _exitRequested = exitRequested;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        BackColor = Color.FromArgb(28, 28, 28);
        Opacity = 1.0;
        Size = new Size(InitialToolbarWidth, 28);
        Padding = new Padding(2);

        _toolTip.InitialDelay = 350;
        _toolTip.ReshowDelay = 100;
        _toolTip.AutoPopDelay = 5000;
        _toolTip.ShowAlways = true;

        BuildButtons();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            var comboBounds = _layoutCombo.RectangleToScreen(_layoutCombo.ClientRectangle);
            var programComboBounds = _programCombo.RectangleToScreen(_programCombo.ClientRectangle);
            if (!comboBounds.Contains(Cursor.Position) &&
                !programComboBounds.Contains(Cursor.Position))
            {
                m.Result = new IntPtr(MA_NOACTIVATE);
                return;
            }
        }

        base.WndProc(ref m);
    }

    public bool UpdatePosition(bool buttonsVisible, bool selectedForDisplay)
    {
        if (!buttonsVisible ||
            !selectedForDisplay ||
            !WindowController.IsOverlayLayout(TargetWindow) ||
            !WindowController.IsMovableWindow(TargetWindow))
        {
            Hide();
            return false;
        }

        if (!WindowController.TryGetWindowRectangle(TargetWindow, out var targetRect))
        {
            Hide();
            return false;
        }

        if (IsComboInteracting())
        {
            return Visible;
        }

        var workingArea = Screen.FromHandle(TargetWindow).WorkingArea;
        var location = GetClampedLocation(targetRect, workingArea);
        var x = location.X;
        var y = location.Y;

        var overlayRect = new Rectangle(x, y, Width, Height);
        if (!WindowController.IsRectangleVisibleOnAnyScreen(overlayRect))
        {
            Hide();
            return false;
        }

        var desiredBounds = new Rectangle(x, y, Width, Height);
        if (Bounds != desiredBounds)
        {
            SetBounds(desiredBounds.X, desiredBounds.Y, desiredBounds.Width, desiredBounds.Height);
        }

        if (!Visible)
        {
            Show();
        }

        WindowController.PlaceWindowDirectlyAbove(
            Handle,
            TargetWindow,
            desiredBounds);
        SyncToggleStates();
        return true;
    }

    public void ShowForPresentation()
    {
        SyncToggleStates();
        var targetScreen = TargetWindow != IntPtr.Zero
            ? Screen.FromHandle(TargetWindow)
            : Screen.PrimaryScreen;
        var screenBounds = targetScreen?.Bounds ?? SystemInformation.VirtualScreen;
        SetBounds(screenBounds.Right - Width - 4, screenBounds.Top + 5, Width, Height);
        if (!Visible)
        {
            Show();
        }

        BringToFront();
    }

    public void EndPresentationMode()
    {
        SyncToggleStates();
    }

    public void SyncToggleStates()
    {
        ApplyToolbarTheme();

        if (_allButton is not null)
        {
            ApplyActiveState(_allButton, _getMoveAllWindows());
            _allButton.Invalidate();
        }

        if (_crosshairButton is not null)
        {
            ApplyActiveState(_crosshairButton, _getCrosshairEnabled());
            _crosshairButton.Invalidate();
        }

        if (_annotationToolsButton is not null)
        {
            _annotationToolsButton.Visible = !_toolbarMinimized && _getShowAnnotationSet();
        }

        SyncLayoutControlsVisibility();
        SyncProgramControlsVisibility();
        ApplyToolbarMinimizedState();
        ApplyButtonConfiguration();
    }

    public bool TryExecuteButton(string id)
    {
        if (!_buttonActions.TryGetValue(id, out var action))
        {
            return false;
        }

        action();
        return true;
    }

    public void RefreshLayoutNames(string? selectedName)
    {
        var currentText = selectedName ?? _layoutCombo.Text;
        var names = _getLayoutNames();

        _layoutCombo.BeginUpdate();
        _layoutCombo.Items.Clear();
        _layoutCombo.Items.AddRange(names.Cast<object>().ToArray());
        _layoutCombo.EndUpdate();
        _layoutCombo.Text = currentText;
        _layoutCombo.BackColor = Color.White;
    }

    public void RefreshProgramNames(string? selectedName)
    {
        var currentText = selectedName ?? _programCombo.Text;
        var names = _getProgramNames();

        _programCombo.BeginUpdate();
        _programCombo.Items.Clear();
        _programCombo.Items.AddRange(names.Cast<object>().ToArray());
        _programCombo.EndUpdate();
        _programCombo.Text = currentText;
        _programCombo.BackColor = Color.White;
    }

    private void BuildButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _toolbarPanel = panel;

        _crosshairButton = CreateWindowIconButton(WindowControlIcon.Crosshair, _toggleCrosshair, false);
        ConfigureButton(_crosshairButton, "main.crosshair", _toggleCrosshair);
        panel.Controls.Add(_crosshairButton);

        _annotationToolsButton = CreateAssetButton(
            "marker_도장",
            () =>
            {
                if (_annotationToolsButton is not null)
                {
                    _showAnnotationTools(_annotationToolsButton.RectangleToScreen(_annotationToolsButton.ClientRectangle));
                }
            },
            26);
        ConfigureButton(
            _annotationToolsButton,
            "main.annotation",
            () => _showAnnotationTools(_annotationToolsButton.RectangleToScreen(_annotationToolsButton.ClientRectangle)));
        panel.Controls.Add(_annotationToolsButton);

        _layoutLeftToggleButton = CreateSetToggleButton(pointsRight: false, _toggleLayoutControls);
        ConfigureButton(_layoutLeftToggleButton, "main.layout_toggle", _toggleLayoutControls);
        panel.Controls.Add(_layoutLeftToggleButton);

        _layoutCombo.Size = new Size(50, 23);
        _layoutCombo.DropDownWidth = 150;
        _layoutCombo.Margin = new Padding(1);
        _layoutCombo.DropDownStyle = ComboBoxStyle.DropDown;
        _layoutCombo.FlatStyle = FlatStyle.Flat;
        _layoutCombo.Font = new Font("Segoe UI", 8.5F);
        panel.Controls.Add(_layoutCombo);
        _layoutControls.Add(_layoutCombo);
        _toolTip.SetToolTip(_layoutCombo, "창 위치 이름 입력 또는 저장 목록 선택");

        var layoutSaveButton = CreateLayoutButton("S", "현재 창 위치 저장 (Save)", _saveLayout);
        ConfigureButton(layoutSaveButton, "main.layout_save", () => _saveLayout(_layoutCombo.Text));
        AddLayoutControl(panel, layoutSaveButton);
        var layoutLoadButton = CreateLayoutButton("L", "선택한 창 위치 불러오기 (Load)", _loadLayout);
        ConfigureButton(layoutLoadButton, "main.layout_load", () => _loadLayout(_layoutCombo.Text));
        AddLayoutControl(panel, layoutLoadButton);
        var layoutDeleteButton = CreateLayoutButton("D", "선택한 창 위치 삭제 (Delete)", _deleteLayout);
        ConfigureButton(layoutDeleteButton, "main.layout_delete", () => _deleteLayout(_layoutCombo.Text));
        AddLayoutControl(panel, layoutDeleteButton);

        _layoutRightToggleButton = CreateSetToggleButton(pointsRight: true, _toggleLayoutControls);
        ConfigureButton(_layoutRightToggleButton, "main.layout_toggle", _toggleLayoutControls);
        panel.Controls.Add(_layoutRightToggleButton);

        _programLeftToggleButton = CreateSetToggleButton(pointsRight: false, _toggleProgramControls);
        ConfigureButton(_programLeftToggleButton, "main.program_toggle", _toggleProgramControls);
        panel.Controls.Add(_programLeftToggleButton);

        _programCombo.Size = new Size(110, 23);
        _programCombo.DropDownWidth = 320;
        _programCombo.MaxDropDownItems = 20;
        _programCombo.Margin = new Padding(1);
        _programCombo.DropDownStyle = ComboBoxStyle.DropDown;
        _programCombo.FlatStyle = FlatStyle.Flat;
        _programCombo.Font = new Font("Segoe UI", 8.5F);
        AddProgramControl(panel, _programCombo);
        _toolTip.SetToolTip(_programCombo, "실행 항목 이름 입력 또는 저장 목록 선택");

        var programSaveButton = CreateProgramButton("S", "실행할 프로그램 또는 파일 등록 (Save)", SaveProgram);
        ConfigureButton(programSaveButton, "main.program_save", SaveProgram);
        AddProgramControl(panel, programSaveButton);
        var programRunButton = CreateProgramButton("R", "선택한 프로그램, 파일 또는 폴더 실행 (Run)", LoadProgram);
        ConfigureButton(programRunButton, "main.program_run", LoadProgram);
        AddProgramControl(panel, programRunButton);
        var programEditButton = CreateProgramButton("E", "표시 이름과 실행 정보 편집 (Edit)", EditProgram);
        ConfigureButton(programEditButton, "main.program_edit", EditProgram);
        AddProgramControl(panel, programEditButton);
        var programDeleteButton = CreateProgramButton("D", "선택한 실행 항목 삭제 (Delete)", DeleteProgram);
        ConfigureButton(programDeleteButton, "main.program_delete", DeleteProgram);
        AddProgramControl(panel, programDeleteButton);

        _programRightToggleButton = CreateSetToggleButton(pointsRight: true, _toggleProgramControls);
        ConfigureButton(_programRightToggleButton, "main.program_toggle", _toggleProgramControls);
        panel.Controls.Add(_programRightToggleButton);

        panel.Controls.Add(CreateMoveButton("main.move_left", WindowControlIcon.ArrowLeft, MoveDirection.Left));
        panel.Controls.Add(CreateMoveButton("main.move_right", WindowControlIcon.ArrowRight, MoveDirection.Right));
        panel.Controls.Add(CreateMoveButton("main.move_up_left", WindowControlIcon.ArrowUpLeft, MoveDirection.UpLeft));
        panel.Controls.Add(CreateMoveButton("main.move_up_right", WindowControlIcon.ArrowUpRight, MoveDirection.UpRight));
        panel.Controls.Add(CreateMoveButton("main.move_down", WindowControlIcon.ArrowDown, MoveDirection.Down));

        panel.Controls.Add(CreateHalfButton("main.half_left", WindowControlIcon.HalfLeft, WindowHalf.Left));
        panel.Controls.Add(CreateHalfButton("main.half_right", WindowControlIcon.HalfRight, WindowHalf.Right));
        panel.Controls.Add(CreateHalfButton("main.half_top", WindowControlIcon.HalfTop, WindowHalf.Top));
        panel.Controls.Add(CreateHalfButton("main.half_bottom", WindowControlIcon.HalfBottom, WindowHalf.Bottom));

        var appExitButton = CreateWindowIconButton(WindowControlIcon.AppExit, _exitRequested, false);
        ConfigureButton(appExitButton, "main.app_exit", _exitRequested);
        panel.Controls.Add(appExitButton);

        var settingsButton = CreateWindowIconButton(WindowControlIcon.Settings, _showAnnotationSettings, false, 24);
        ConfigureButton(settingsButton, "main.settings", _showAnnotationSettings);
        panel.Controls.Add(settingsButton);

        _allButton = CreateFlatButton("ALL", 34);
        _allButton.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _allButton.Click += (_, _) => _toggleMoveAllWindows();
        ConfigureButton(_allButton, "main.move_all", _toggleMoveAllWindows);
        panel.Controls.Add(_allButton);

        var minimizeButton = CreateWindowIconButton(
            WindowControlIcon.Minimize,
            () => WindowController.ApplyAction(TargetWindow, WindowAction.Minimize));
        ConfigureButton(minimizeButton, "main.minimize", () => WindowController.ApplyAction(TargetWindow, WindowAction.Minimize));
        panel.Controls.Add(minimizeButton);

        var maximizeButton = CreateWindowIconButton(
            WindowControlIcon.Maximize,
            () => WindowController.ToggleMaximizeWindow(TargetWindow));
        ConfigureButton(maximizeButton, "main.maximize", () => WindowController.ToggleMaximizeWindow(TargetWindow));
        panel.Controls.Add(maximizeButton);

        var closeButton = CreateWindowIconButton(
            WindowControlIcon.Close,
            () => WindowController.CloseWindow(TargetWindow));
        ConfigureButton(closeButton, "main.close", () => WindowController.CloseWindow(TargetWindow));
        panel.Controls.Add(closeButton);

        _toolbarMinimizeButton = CreateAssetButton("tool_minimize", ToggleToolbarMinimized, 14);
        _toolbarMinimizeButton.Margin = new Padding(1, 1, 0, 1);
        ConfigureButton(_toolbarMinimizeButton, "main.collapse", ToggleToolbarMinimized);
        panel.Controls.Add(_toolbarMinimizeButton);

        Controls.Add(panel);
        RefreshLayoutNames(null);
        RefreshProgramNames(null);
        SyncLayoutControlsVisibility();
        SyncProgramControlsVisibility();
    }

    private void AddLayoutControl(FlowLayoutPanel panel, Control control)
    {
        panel.Controls.Add(control);
        _layoutControls.Add(control);
    }

    private Button CreateSetToggleButton(bool pointsRight, Action toggleAction)
    {
        var button = CreateFlatButton(string.Empty, 22);
        button.FlatAppearance.BorderColor = Color.FromArgb(72, 171, 124);
        button.Paint += (_, e) => IconAssets.Draw(
            e.Graphics,
            button.ClientRectangle,
            pointsRight ? "set_expand_right" : "set_expand_left",
            Color.FromArgb(72, 171, 124),
            _getSharpIconRendering());
        button.Click += (_, _) => toggleAction();
        return button;
    }

    private void SyncLayoutControlsVisibility()
    {
        var shown = _getShowLayoutSet();
        var expanded = shown && _getLayoutControlsExpanded();
        _toolbarPanel?.SuspendLayout();
        foreach (var toggleButton in new[] { _layoutLeftToggleButton, _layoutRightToggleButton })
        {
            if (toggleButton is not null)
            {
                toggleButton.Visible = !_toolbarMinimized && shown;
            }
        }

        foreach (var control in _layoutControls)
        {
            control.Visible = !_toolbarMinimized && expanded;
        }

        foreach (var toggleButton in new[] { _layoutLeftToggleButton, _layoutRightToggleButton })
        {
            if (toggleButton is null)
            {
                continue;
            }

            toggleButton.Visible = !_toolbarMinimized && shown;
            _toolTip.SetToolTip(
                toggleButton,
                expanded ? "창 위치 저장 세트 숨기기" : "창 위치 저장 세트 펼치기");
            toggleButton.Invalidate();
        }
        _toolbarPanel?.ResumeLayout(true);
        UpdateToolbarWidth();
    }

    private Button CreateCommandButton(string text, int width, Action action)
    {
        var button = CreateFlatButton(text, width);
        button.Click += (_, _) => action();
        return button;
    }

    private void UpdateToolbarWidth()
    {
        if (_toolbarMinimized && _toolbarMinimizeButton is not null)
        {
            Width = Padding.Horizontal + _toolbarMinimizeButton.Width + _toolbarMinimizeButton.Margin.Horizontal;
            return;
        }

        if (_toolbarPanel is null)
        {
            return;
        }

        _toolbarPanel.PerformLayout();
        var contentWidth = _toolbarPanel.Controls
            .Cast<Control>()
            .Where(control => control.Visible)
            .Sum(control => control.Width + control.Margin.Horizontal);
        Width = Padding.Horizontal + _toolbarPanel.Padding.Horizontal + contentWidth;
    }

    private void ToggleToolbarMinimized()
    {
        SetToolbarMinimized(!_toolbarMinimized);
    }

    private void SetToolbarMinimized(bool minimized)
    {
        if (_toolbarMinimized == minimized)
        {
            return;
        }

        var right = Right;
        _toolbarMinimized = minimized;
        if (_toolbarMinimized)
        {
            ApplyToolbarMinimizedState();
        }
        else
        {
            foreach (var control in GetToolbarControls())
            {
                control.Visible = true;
            }

            _toolTip.SetToolTip(_toolbarMinimizeButton, _getButtonName("main.collapse"));
            SyncToggleStates();
        }

        Left = right - Width;
        RepositionWithinTargetScreen();
    }

    private void ApplyToolbarMinimizedState()
    {
        if (!_toolbarMinimized || _toolbarMinimizeButton is null)
        {
            return;
        }

        foreach (var control in GetToolbarControls())
        {
            control.Visible = control == _toolbarMinimizeButton;
        }

        UpdateToolbarWidth();
        _toolbarMinimizeButton.Visible = true;
        _toolTip.SetToolTip(_toolbarMinimizeButton, _getButtonName("main.collapse"));
    }

    private IEnumerable<Control> GetToolbarControls()
    {
        return Controls
            .OfType<FlowLayoutPanel>()
            .SelectMany(panel => panel.Controls.Cast<Control>());
    }

    private void RepositionWithinTargetScreen()
    {
        if (!WindowController.TryGetWindowRectangle(TargetWindow, out var targetRect))
        {
            return;
        }

        var workingArea = Screen.FromHandle(TargetWindow).WorkingArea;
        Location = GetClampedLocation(targetRect, workingArea);
    }

    private Point GetClampedLocation(Rectangle targetRect, Rectangle workingArea)
    {
        var right = workingArea.Right - ToolbarEdgeInset;
        var x = Math.Clamp(
            right - Width,
            workingArea.Left,
            Math.Max(workingArea.Left, workingArea.Right - Width));
        var y = workingArea.Top;
        return new Point(x, y);
    }

    private void AddProgramControl(FlowLayoutPanel panel, Control control)
    {
        panel.Controls.Add(control);
        _programControls.Add(control);
    }

    private void SyncProgramControlsVisibility()
    {
        var shown = _getShowProgramSet();
        var expanded = shown && _getProgramControlsExpanded();
        _toolbarPanel?.SuspendLayout();
        foreach (var toggleButton in new[] { _programLeftToggleButton, _programRightToggleButton })
        {
            if (toggleButton is not null)
            {
                toggleButton.Visible = !_toolbarMinimized && shown;
            }
        }

        foreach (var control in _programControls)
        {
            control.Visible = !_toolbarMinimized && expanded;
        }

        foreach (var toggleButton in new[] { _programLeftToggleButton, _programRightToggleButton })
        {
            if (toggleButton is null)
            {
                continue;
            }

            toggleButton.Visible = !_toolbarMinimized && shown;
            _toolTip.SetToolTip(toggleButton, expanded ? "프로그램 실행 세트 숨기기" : "프로그램 실행 세트 펼치기");
            toggleButton.Invalidate();
        }
        _toolbarPanel?.ResumeLayout(true);
        UpdateToolbarWidth();
    }

    private bool IsComboInteracting()
    {
        return _layoutCombo.DroppedDown ||
               _programCombo.DroppedDown ||
               _layoutCombo.ContainsFocus ||
               _programCombo.ContainsFocus;
    }

    private Button CreateProgramButton(string text, string toolTip, Action action)
    {
        var button = CreateFlatButton(text, 18);
        button.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        button.Click += (_, _) => action();
        _toolTip.SetToolTip(button, toolTip);
        return button;
    }

    private void SaveProgram()
    {
        var savedName = _saveProgram(_programCombo.Text);
        _programCombo.BackColor = savedName is null ? Color.MistyRose : Color.White;
        if (savedName is not null)
        {
            RefreshProgramNames(savedName);
        }
    }

    private void LoadProgram()
    {
        _programCombo.BackColor = _loadProgram(_programCombo.Text) ? Color.White : Color.MistyRose;
    }

    private void DeleteProgram()
    {
        var succeeded = _deleteProgram(_programCombo.Text);
        _programCombo.BackColor = succeeded ? Color.White : Color.MistyRose;
        if (succeeded)
        {
            RefreshProgramNames(string.Empty);
        }
    }

    private void EditProgram()
    {
        var editedName = _editProgram(_programCombo.Text);
        _programCombo.BackColor = editedName is null ? Color.MistyRose : Color.White;
        if (editedName is not null)
        {
            RefreshProgramNames(editedName);
        }
    }

    private void ApplyToolbarTheme()
    {
        var background = GetEffectiveToolbarColor();
        var foreground = GetContrastColor(background);
        var hover = BlendForInteraction(background, 0.16F);
        var pressed = BlendForInteraction(background, 0.28F);
        BackColor = background;

        foreach (var button in Controls.OfType<FlowLayoutPanel>().SelectMany(panel => panel.Controls.OfType<Button>()))
        {
            button.BackColor = background;
            button.ForeColor = foreground;
            button.FlatAppearance.MouseOverBackColor = hover;
            button.FlatAppearance.MouseDownBackColor = pressed;
            if (button != _layoutLeftToggleButton && button != _layoutRightToggleButton &&
                button != _programLeftToggleButton && button != _programRightToggleButton)
            {
                button.FlatAppearance.BorderColor = BlendForInteraction(background, 0.22F);
            }
        }
    }

    private static void ApplyActiveState(Button button, bool active)
    {
        if (!active)
        {
            return;
        }

        button.BackColor = Color.FromArgb(0, 105, 145);
        button.ForeColor = Color.White;
    }

    private Color GetEffectiveToolbarColor()
    {
        if (!_getMatchTargetWindowColor() || !TrySampleTargetWindowColor(out var sampled))
        {
            return _getToolbarColor();
        }

        return sampled;
    }

    private bool TrySampleTargetWindowColor(out Color color)
    {
        if (_sampledTargetColor is not null && DateTime.UtcNow < _nextTargetColorSampleUtc)
        {
            color = _sampledTargetColor.Value;
            return true;
        }

        color = default;
        if (!WindowController.TryGetWindowRectangle(TargetWindow, out var targetRect) ||
            targetRect.Width < 40 || targetRect.Height < 20)
        {
            return false;
        }

        try
        {
            const int sampleSize = 9;
            var centerX = targetRect.Left + Math.Clamp(targetRect.Width / 4, 20, targetRect.Width - 20);
            var centerY = targetRect.Top + Math.Clamp(10, 4, targetRect.Height - 4);
            using var bitmap = new Bitmap(sampleSize, sampleSize);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(
                    centerX - sampleSize / 2,
                    centerY - sampleSize / 2,
                    0,
                    0,
                    bitmap.Size,
                    CopyPixelOperation.SourceCopy);
            }

            long red = 0;
            long green = 0;
            long blue = 0;
            for (var y = 0; y < sampleSize; y++)
            {
                for (var x = 0; x < sampleSize; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    red += pixel.R;
                    green += pixel.G;
                    blue += pixel.B;
                }
            }

            var count = sampleSize * sampleSize;
            color = Color.FromArgb((int)(red / count), (int)(green / count), (int)(blue / count));
            _sampledTargetColor = color;
            _nextTargetColorSampleUtc = DateTime.UtcNow.AddSeconds(1);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Color GetContrastColor(Color background)
    {
        var luminance = 0.299 * background.R + 0.587 * background.G + 0.114 * background.B;
        return luminance >= 155 ? Color.Black : Color.White;
    }

    private static Color BlendForInteraction(Color color, float amount)
    {
        var target = GetContrastColor(color) == Color.White ? Color.White : Color.Black;
        return Color.FromArgb(
            (int)(color.R + (target.R - color.R) * amount),
            (int)(color.G + (target.G - color.G) * amount),
            (int)(color.B + (target.B - color.B) * amount));
    }

    private Button CreateMoveButton(string id, WindowControlIcon icon, MoveDirection direction)
    {
        var button = CreateWindowIconButton(icon, () => MoveTargets(direction), false);
        var toolTip = direction switch
        {
            MoveDirection.Left => "왼쪽 모니터로 이동",
            MoveDirection.Right => "오른쪽 모니터로 이동",
            MoveDirection.UpLeft => "왼쪽 위 모니터로 이동",
            MoveDirection.UpRight => "오른쪽 위 모니터로 이동",
            MoveDirection.Down => "아래 모니터로 이동",
            _ => "창 이동"
        };
        ConfigureButton(button, id, () => MoveTargets(direction));
        return button;
    }

    private Button CreateHalfButton(string id, WindowControlIcon icon, WindowHalf half)
    {
        var button = CreateWindowIconButton(icon, () => WindowController.SnapWindowToHalf(TargetWindow, half), width: 24);
        var toolTip = half switch
        {
            WindowHalf.Left => "현재 모니터 왼쪽 절반 배치",
            WindowHalf.Right => "현재 모니터 오른쪽 절반 배치",
            WindowHalf.Top => "현재 모니터 위쪽 절반 배치",
            WindowHalf.Bottom => "현재 모니터 아래쪽 절반 배치",
            _ => "현재 모니터 절반 배치"
        };
        ConfigureButton(button, id, () => WindowController.SnapWindowToHalf(TargetWindow, half));
        return button;
    }

    private Button CreateLayoutButton(string text, string toolTip, Func<string, bool> action)
    {
        var button = CreateFlatButton(text, 18);
        button.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        button.Click += (_, _) =>
        {
            var succeeded = action(_layoutCombo.Text);
            _layoutCombo.BackColor = succeeded ? Color.White : Color.MistyRose;
            WindowController.ActivateWindow(TargetWindow);
        };
        _toolTip.SetToolTip(button, toolTip);
        return button;
    }

    private Button CreateWindowIconButton(
        WindowControlIcon icon,
        Action action,
        bool requiresTarget = true,
        int width = 26)
    {
        var button = CreateFlatButton(string.Empty, width);
        button.Paint += (_, e) => IconAssets.Draw(
            e.Graphics,
            button.ClientRectangle,
            GetIconAssetName(icon),
            button.ForeColor,
            _getSharpIconRendering());
        button.Click += (_, _) =>
        {
            if (!requiresTarget || (TargetWindow != IntPtr.Zero && WindowController.IsMovableWindow(TargetWindow)))
            {
                action();
            }
        };
        return button;
    }

    private Button CreateAssetButton(string assetName, Action action, int width = 26)
    {
        var button = CreateFlatButton(string.Empty, width);
        button.Paint += (_, e) => IconAssets.Draw(
            e.Graphics,
            button.ClientRectangle,
            assetName,
            button.ForeColor,
            _getSharpIconRendering());
        button.Click += (_, _) => action();
        return button;
    }

    private void ConfigureButton(Control control, string id, Action action)
    {
        if (!_configuredControls.TryGetValue(id, out var controls))
        {
            controls = new List<Control>();
            _configuredControls[id] = controls;
        }
        controls.Add(control);
        _buttonActions[id] = action;
        _toolTip.SetToolTip(control, _getButtonName(id));
    }

    private void ApplyButtonConfiguration()
    {
        foreach (var pair in _configuredControls)
        {
            var definition = ButtonCatalog.Get(pair.Key);
            var preference = _getButtonPreference(pair.Key);
            var visible = definition.Required || preference.Visible;
            if (pair.Key == "main.annotation")
            {
                visible &= _getShowAnnotationSet();
            }
            foreach (var control in pair.Value)
            {
                var followsSetState =
                    pair.Key.StartsWith("main.layout_", StringComparison.Ordinal) ||
                    pair.Key.StartsWith("main.program_", StringComparison.Ordinal);
                var configuredVisible = visible && (!followsSetState || control.Visible);
                control.Visible = _toolbarMinimized
                    ? pair.Key == "main.collapse"
                    : configuredVisible;
                _toolTip.SetToolTip(control, _getButtonName(pair.Key));
            }
        }
        UpdateToolbarWidth();
    }

    private static string GetIconAssetName(WindowControlIcon icon)
    {
        return icon switch
        {
            WindowControlIcon.Minimize => "window_minimize",
            WindowControlIcon.Maximize => "window_restore",
            WindowControlIcon.Close => "window_close",
            WindowControlIcon.AppExit => "app_exit",
            WindowControlIcon.HalfLeft => "half_left",
            WindowControlIcon.HalfRight => "half_right",
            WindowControlIcon.HalfTop => "half_top",
            WindowControlIcon.HalfBottom => "half_bottom",
            WindowControlIcon.ArrowLeft => "move_left",
            WindowControlIcon.ArrowRight => "move_right",
            WindowControlIcon.ArrowUpLeft => "move_up_left",
            WindowControlIcon.ArrowUpRight => "move_up_right",
            WindowControlIcon.ArrowDown => "move_down",
            WindowControlIcon.Crosshair => "crosshair",
            WindowControlIcon.Capture => "capture",
            WindowControlIcon.Settings => "settings",
            WindowControlIcon.Pencil => "pencil",
            _ => "settings"
        };
    }

    private static Button CreateFlatButton(string text, int width = 26)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(width, 23),
            Margin = new Padding(1),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TabStop = false
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 70);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(90, 90, 90);
        return button;
    }

    private static void DrawWindowControlIcon(Graphics graphics, Rectangle bounds, WindowControlIcon icon, Color iconColor)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        using var pen = new Pen(iconColor, 2F)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Square,
            EndCap = System.Drawing.Drawing2D.LineCap.Square,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Miter
        };

        switch (icon)
        {
            case WindowControlIcon.Minimize:
                graphics.DrawLine(pen, 7, 15, bounds.Width - 7, 15);
                break;
            case WindowControlIcon.Maximize:
                graphics.DrawRectangle(pen, 9, 5, bounds.Width - 16, 10);
                graphics.DrawRectangle(pen, 6, 8, bounds.Width - 16, 10);
                break;
            case WindowControlIcon.Close:
                graphics.DrawLine(pen, 8, 6, bounds.Width - 8, 17);
                graphics.DrawLine(pen, bounds.Width - 8, 6, 8, 17);
                break;
            case WindowControlIcon.ArrowLeft:
            case WindowControlIcon.ArrowRight:
            case WindowControlIcon.ArrowUpLeft:
            case WindowControlIcon.ArrowUpRight:
            case WindowControlIcon.ArrowDown:
                DrawArrowIcon(graphics, bounds, pen, icon);
                break;
            case WindowControlIcon.Crosshair:
                graphics.DrawLine(pen, bounds.Width / 2, 5, bounds.Width / 2, 18);
                graphics.DrawLine(pen, 6, 11, bounds.Width - 6, 11);
                break;
            case WindowControlIcon.Capture:
                graphics.DrawRectangle(pen, 5, 6, bounds.Width - 11, 11);
                break;
            case WindowControlIcon.Settings:
                graphics.DrawEllipse(pen, bounds.Width / 2 - 5, 6, 10, 10);
                graphics.DrawEllipse(pen, bounds.Width / 2 - 1, 10, 2, 2);
                break;
            case WindowControlIcon.Pencil:
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.DrawLine(pen, 7, 16, bounds.Width - 7, 6);
                graphics.DrawLine(pen, 8, 17, 5, 18);
                graphics.DrawLine(pen, 7, 14, 10, 17);
                break;
            case WindowControlIcon.HalfLeft:
            case WindowControlIcon.HalfRight:
            case WindowControlIcon.HalfTop:
            case WindowControlIcon.HalfBottom:
                DrawHalfIcon(graphics, bounds, pen, icon);
                break;
        }
    }

    private static void DrawLayoutToggleIcon(Graphics graphics, Rectangle bounds, bool pointsRight)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        using var pen = new Pen(Color.FromArgb(72, 171, 124), 2F);
        var centerX = bounds.Width / 2;

        if (!pointsRight)
        {
            graphics.DrawLine(pen, centerX + 3, 6, centerX - 2, 11);
            graphics.DrawLine(pen, centerX - 2, 11, centerX + 3, 16);
        }
        else
        {
            graphics.DrawLine(pen, centerX - 3, 6, centerX + 2, 11);
            graphics.DrawLine(pen, centerX + 2, 11, centerX - 3, 16);
        }
    }

    private static void DrawArrowIcon(Graphics graphics, Rectangle bounds, Pen pen, WindowControlIcon icon)
    {
        var centerX = bounds.Width / 2;
        switch (icon)
        {
            case WindowControlIcon.ArrowLeft:
                graphics.DrawLine(pen, 7, 11, bounds.Width - 7, 11);
                graphics.DrawLine(pen, 7, 11, 11, 7);
                graphics.DrawLine(pen, 7, 11, 11, 15);
                break;
            case WindowControlIcon.ArrowRight:
                graphics.DrawLine(pen, 7, 11, bounds.Width - 7, 11);
                graphics.DrawLine(pen, bounds.Width - 7, 11, bounds.Width - 11, 7);
                graphics.DrawLine(pen, bounds.Width - 7, 11, bounds.Width - 11, 15);
                break;
            case WindowControlIcon.ArrowUpLeft:
                graphics.DrawLine(pen, 8, 6, bounds.Width - 8, 16);
                graphics.DrawLine(pen, 8, 6, 8, 11);
                graphics.DrawLine(pen, 8, 6, 13, 6);
                break;
            case WindowControlIcon.ArrowUpRight:
                graphics.DrawLine(pen, bounds.Width - 8, 6, 8, 16);
                graphics.DrawLine(pen, bounds.Width - 8, 6, bounds.Width - 8, 11);
                graphics.DrawLine(pen, bounds.Width - 8, 6, bounds.Width - 13, 6);
                break;
            case WindowControlIcon.ArrowDown:
                graphics.DrawLine(pen, centerX, 6, centerX, 16);
                graphics.DrawLine(pen, centerX, 16, centerX - 4, 12);
                graphics.DrawLine(pen, centerX, 16, centerX + 4, 12);
                break;
        }
    }

    private static void DrawHalfIcon(Graphics graphics, Rectangle bounds, Pen pen, WindowControlIcon icon)
    {
        var frame = new Rectangle(5, 5, bounds.Width - 11, 12);
        var inner = Rectangle.Inflate(frame, -2, -2);
        var fill = icon switch
        {
            WindowControlIcon.HalfLeft => new Rectangle(inner.Left, inner.Top, inner.Width / 2, inner.Height),
            WindowControlIcon.HalfRight => new Rectangle(inner.Left + inner.Width / 2, inner.Top, inner.Width - inner.Width / 2, inner.Height),
            WindowControlIcon.HalfTop => new Rectangle(inner.Left, inner.Top, inner.Width, inner.Height / 2),
            WindowControlIcon.HalfBottom => new Rectangle(inner.Left, inner.Top + inner.Height / 2, inner.Width, inner.Height - inner.Height / 2),
            _ => Rectangle.Empty
        };

        graphics.DrawRectangle(pen, frame);
        using var brush = new SolidBrush(pen.Color);
        graphics.FillRectangle(brush, fill);
    }

    private enum WindowControlIcon
    {
        Minimize,
        Maximize,
        Close,
        AppExit,
        HalfLeft,
        HalfRight,
        HalfTop,
        HalfBottom,
        ArrowLeft,
        ArrowRight,
        ArrowUpLeft,
        ArrowUpRight,
        ArrowDown,
        Crosshair,
        Capture,
        Settings,
        Pencil
    }

    private void MoveTargets(MoveDirection direction)
    {
        var targets = _getMoveAllWindows()
            ? WindowController.GetMovableWindows().Select(window => window.Handle).ToList()
            : GetSingleTarget();

        foreach (var target in targets)
        {
            WindowController.MoveWindowToDirection(target, direction);
        }
    }

    private IReadOnlyList<IntPtr> GetSingleTarget()
    {
        return TargetWindow != IntPtr.Zero && WindowController.IsMovableWindow(TargetWindow)
            ? new[] { TargetWindow }
            : Array.Empty<IntPtr>();
    }
}
