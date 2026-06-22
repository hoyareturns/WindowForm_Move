namespace WindowForm_Move;

public sealed class OverlayForm : Form
{
    private const int CoreWidth = 468;
    private const int SetToggleAddition = 48;
    private const int LayoutExpandedAddition = 112;
    private const int AnnotationExpandedAddition = 312;
    private const int ProgramExpandedAddition = 192;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    private readonly Func<bool> _getMoveAllWindows;
    private readonly Action _toggleMoveAllWindows;
    private readonly Func<bool> _getCrosshairEnabled;
    private readonly Action _toggleCrosshair;
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
    private readonly Action _exitRequested;
    private readonly ComboBox _layoutCombo = new();
    private readonly ComboBox _programCombo = new();
    private readonly ToolTip _toolTip = new();
    private readonly List<Control> _layoutControls = new();
    private readonly List<Control> _annotationControls = new();
    private readonly List<Control> _programControls = new();
    private bool _updatingMarkerNumber;
    private Color? _sampledTargetColor;
    private DateTime _nextTargetColorSampleUtc;
    private Button? _allButton;
    private Button? _crosshairButton;
    private Button? _layoutLeftToggleButton;
    private Button? _layoutRightToggleButton;
    private Button? _annotationLeftToggleButton;
    private Button? _annotationRightToggleButton;
    private Button? _programLeftToggleButton;
    private Button? _programRightToggleButton;
    private Button? _markerButton;
    private Button? _dotButton;
    private Button? _arrowMemoButton;
    private Button? _pencilButton;
    private Button? _eraserButton;
    private Button? _markerColorButton;
    private Button? _penColorButton;
    private NumericUpDown? _markerNumberInput;

    public IntPtr TargetWindow { get; }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            return cp;
        }
    }

    public OverlayForm(
        IntPtr targetWindow,
        Func<bool> getMoveAllWindows,
        Action toggleMoveAllWindows,
        Func<bool> getCrosshairEnabled,
        Action toggleCrosshair,
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
        Action exitRequested)
    {
        TargetWindow = targetWindow;
        _getMoveAllWindows = getMoveAllWindows;
        _toggleMoveAllWindows = toggleMoveAllWindows;
        _getCrosshairEnabled = getCrosshairEnabled;
        _toggleCrosshair = toggleCrosshair;
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
        _exitRequested = exitRequested;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(28, 28, 28);
        Opacity = 1.0;
        Size = new Size(CoreWidth, 28);
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
            var markerNumberBounds = _markerNumberInput?.RectangleToScreen(_markerNumberInput.ClientRectangle) ?? Rectangle.Empty;
            if (!comboBounds.Contains(Cursor.Position) &&
                !programComboBounds.Contains(Cursor.Position) &&
                !markerNumberBounds.Contains(Cursor.Position))
            {
                m.Result = new IntPtr(MA_NOACTIVATE);
                return;
            }
        }

        base.WndProc(ref m);
    }

    public bool UpdatePosition(bool buttonsVisible)
    {
        if (!buttonsVisible || !WindowController.IsOverlayLayout(TargetWindow) || !WindowController.IsMovableWindow(TargetWindow))
        {
            Hide();
            return false;
        }

        if (!WindowController.TryGetWindowRectangle(TargetWindow, out var targetRect))
        {
            Hide();
            return false;
        }

        if (targetRect.Width < Width + 8 || targetRect.Height < Height + 8)
        {
            Hide();
            return false;
        }

        var x = targetRect.Right - Width - 4;
        var y = targetRect.Top + 5;

        if (x < targetRect.Left + 4)
        {
            x = targetRect.Left + 4;
        }

        var overlayRect = new Rectangle(x, y, Width, Height);
        if (!targetRect.Contains(overlayRect) || !WindowController.IsRectangleVisibleOnAnyScreen(overlayRect))
        {
            Hide();
            return false;
        }

        if (WindowController.IsCoveredByWindowInFront(TargetWindow, overlayRect))
        {
            Hide();
            return false;
        }

        SetBounds(x, y, Width, Height);

        if (!Visible)
        {
            Show();
        }

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

        if (_markerColorButton is not null)
        {
            _markerColorButton.BackColor = _getMarkerColor();
            _markerColorButton.Invalidate();
        }

        if (_penColorButton is not null)
        {
            _penColorButton.BackColor = _getPenColor();
            _penColorButton.Invalidate();
        }

        if (_markerNumberInput is not null)
        {
            var nextNumber = Math.Clamp(_getNextMarkerNumber(), 1, 9999);
            if (_markerNumberInput.Value != nextNumber)
            {
                _updatingMarkerNumber = true;
                try
                {
                    _markerNumberInput.Value = nextNumber;
                }
                finally
                {
                    _updatingMarkerNumber = false;
                }
            }
        }

        if (_markerButton is not null)
        {
            ApplyActiveState(_markerButton, _getAnnotationTool() == AnnotationTool.Marker);
        }

        if (_dotButton is not null)
        {
            ApplyActiveState(_dotButton, _getAnnotationTool() == AnnotationTool.Dot);
        }

        if (_arrowMemoButton is not null)
        {
            ApplyActiveState(_arrowMemoButton, _getAnnotationTool() == AnnotationTool.Arrow);
        }

        if (_pencilButton is not null)
        {
            ApplyActiveState(_pencilButton, _getAnnotationTool() == AnnotationTool.Pencil);
        }

        if (_eraserButton is not null)
        {
            ApplyActiveState(_eraserButton, _getAnnotationTool() == AnnotationTool.Eraser);
        }

        SyncLayoutControlsVisibility();
        SyncProgramControlsVisibility();
        SyncAnnotationControlsVisibility();
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

        _crosshairButton = CreateWindowIconButton(WindowControlIcon.Crosshair, _toggleCrosshair, false);
        _toolTip.SetToolTip(_crosshairButton, "십자선 가이드 켜기/끄기");
        panel.Controls.Add(_crosshairButton);

        _annotationLeftToggleButton = CreateSetToggleButton(pointsRight: false, _toggleAnnotationControls);
        panel.Controls.Add(_annotationLeftToggleButton);

        _markerColorButton = CreateFlatButton(string.Empty, 24);
        _markerColorButton.FlatAppearance.BorderColor = Color.White;
        _markerColorButton.Click += (_, _) => _chooseMarkerColor();
        AddAnnotationControl(panel, _markerColorButton, "마커 색상 선택");

        _markerNumberInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 9999,
            Value = Math.Clamp(_getNextMarkerNumber(), 1, 9999),
            Size = new Size(48, 23),
            Margin = new Padding(1),
            Font = new Font("Segoe UI", 8F),
            TextAlign = HorizontalAlignment.Center,
            BorderStyle = BorderStyle.FixedSingle,
            TabStop = true
        };
        _markerNumberInput.ValueChanged += (_, _) =>
        {
            if (!_updatingMarkerNumber)
            {
                _setNextMarkerNumber((int)_markerNumberInput.Value);
            }
        };
        foreach (Control child in _markerNumberInput.Controls)
        {
            child.TextChanged += MarkerNumberTextChanged;
        }
        AddAnnotationControl(panel, _markerNumberInput, "다음 마커 번호 입력 (마킹 후 자동 증가)");

        _dotButton = CreateFlatButton("●", 24);
        _dotButton.Click += (_, _) => _toggleAnnotationTool(AnnotationTool.Dot);
        AddAnnotationControl(panel, _dotButton, "번호 없는 원형 포인트");

        _markerButton = CreateFlatButton("①", 24);
        _markerButton.Click += (_, _) => _toggleAnnotationTool(AnnotationTool.Marker);
        AddAnnotationControl(panel, _markerButton, "번호 마커 찍기");

        _penColorButton = CreateFlatButton(string.Empty, 24);
        _penColorButton.FlatAppearance.BorderColor = Color.White;
        _penColorButton.Click += (_, _) => _choosePenColor();
        AddAnnotationControl(panel, _penColorButton, "연필과 화살표 색상 선택");

        _arrowMemoButton = CreateFlatButton("↗", 24);
        _arrowMemoButton.Click += (_, _) => _toggleAnnotationTool(AnnotationTool.Arrow);
        AddAnnotationControl(panel, _arrowMemoButton, "드래그로 화살표+메모 추가, 기존 메모 클릭 시 수정");

        _pencilButton = CreateWindowIconButton(
            WindowControlIcon.Pencil,
            () => _toggleAnnotationTool(AnnotationTool.Pencil),
            false,
            24);
        AddAnnotationControl(panel, _pencilButton, "연필로 자유선 그리기");

        AddAnnotationControl(panel, CreateCommandButton("↶", 24, _undoAnnotation), "마지막 마커 또는 화살표 실행취소");

        _eraserButton = CreateFlatButton("E", 22);
        _eraserButton.Click += (_, _) => _toggleAnnotationTool(AnnotationTool.Eraser);
        AddAnnotationControl(panel, _eraserButton, "마커 또는 화살표 메모 지우기 (Eraser)");
        AddAnnotationControl(panel, CreateCommandButton("AC", 28, _clearAnnotations), "모든 마커와 선 지우기");
        AddAnnotationControl(panel, CreateWindowIconButton(WindowControlIcon.Capture, _captureSelectedRegion, false, 24), "드래그한 영역을 PNG로 저장");

        _annotationRightToggleButton = CreateSetToggleButton(pointsRight: true, _toggleAnnotationControls);
        panel.Controls.Add(_annotationRightToggleButton);

        _layoutLeftToggleButton = CreateSetToggleButton(pointsRight: false, _toggleLayoutControls);
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

        AddLayoutControl(panel, CreateLayoutButton("S", "현재 창 위치 저장 (Save)", _saveLayout));
        AddLayoutControl(panel, CreateLayoutButton("L", "선택한 창 위치 불러오기 (Load)", _loadLayout));
        AddLayoutControl(panel, CreateLayoutButton("D", "선택한 창 위치 삭제 (Delete)", _deleteLayout));

        _layoutRightToggleButton = CreateSetToggleButton(pointsRight: true, _toggleLayoutControls);
        panel.Controls.Add(_layoutRightToggleButton);

        _programLeftToggleButton = CreateSetToggleButton(pointsRight: false, _toggleProgramControls);
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

        AddProgramControl(panel, CreateProgramButton("S", "실행할 프로그램 또는 파일 등록 (Save)", SaveProgram));
        AddProgramControl(panel, CreateProgramButton("R", "선택한 프로그램, 파일 또는 폴더 실행 (Run)", LoadProgram));
        AddProgramControl(panel, CreateProgramButton("E", "표시 이름과 실행 정보 편집 (Edit)", EditProgram));
        AddProgramControl(panel, CreateProgramButton("D", "선택한 실행 항목 삭제 (Delete)", DeleteProgram));

        _programRightToggleButton = CreateSetToggleButton(pointsRight: true, _toggleProgramControls);
        panel.Controls.Add(_programRightToggleButton);

        panel.Controls.Add(CreateMoveButton(WindowControlIcon.ArrowLeft, MoveDirection.Left));
        panel.Controls.Add(CreateMoveButton(WindowControlIcon.ArrowRight, MoveDirection.Right));
        panel.Controls.Add(CreateMoveButton(WindowControlIcon.ArrowUpLeft, MoveDirection.UpLeft));
        panel.Controls.Add(CreateMoveButton(WindowControlIcon.ArrowUpRight, MoveDirection.UpRight));
        panel.Controls.Add(CreateMoveButton(WindowControlIcon.ArrowDown, MoveDirection.Down));

        panel.Controls.Add(CreateHalfButton(WindowControlIcon.HalfLeft, WindowHalf.Left));
        panel.Controls.Add(CreateHalfButton(WindowControlIcon.HalfRight, WindowHalf.Right));
        panel.Controls.Add(CreateHalfButton(WindowControlIcon.HalfTop, WindowHalf.Top));
        panel.Controls.Add(CreateHalfButton(WindowControlIcon.HalfBottom, WindowHalf.Bottom));

        var appExitButton = CreateWindowIconButton(WindowControlIcon.Close, _exitRequested, false);
        _toolTip.SetToolTip(appExitButton, "WindowForm_Move 종료");
        panel.Controls.Add(appExitButton);

        var settingsButton = CreateWindowIconButton(WindowControlIcon.Settings, _showAnnotationSettings, false, 24);
        _toolTip.SetToolTip(settingsButton, "WindowForm_Move 통합 설정");
        panel.Controls.Add(settingsButton);

        _allButton = CreateFlatButton("ALL", 34);
        _allButton.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _allButton.Click += (_, _) => _toggleMoveAllWindows();
        _toolTip.SetToolTip(_allButton, "모든 실행 창을 함께 이동");
        panel.Controls.Add(_allButton);

        var minimizeButton = CreateWindowIconButton(
            WindowControlIcon.Minimize,
            () => WindowController.ApplyAction(TargetWindow, WindowAction.Minimize));
        _toolTip.SetToolTip(minimizeButton, "현재 창 최소화");
        panel.Controls.Add(minimizeButton);

        var maximizeButton = CreateWindowIconButton(
            WindowControlIcon.Maximize,
            () => WindowController.ToggleMaximizeWindow(TargetWindow));
        _toolTip.SetToolTip(maximizeButton, "현재 창 최대화/복원");
        panel.Controls.Add(maximizeButton);

        var closeButton = CreateWindowIconButton(
            WindowControlIcon.Close,
            () => WindowController.CloseWindow(TargetWindow));
        _toolTip.SetToolTip(closeButton, "현재 창 닫기");
        panel.Controls.Add(closeButton);

        Controls.Add(panel);
        RefreshLayoutNames(null);
        RefreshProgramNames(null);
        SyncLayoutControlsVisibility();
        SyncProgramControlsVisibility();
        SyncAnnotationControlsVisibility();
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
        button.Paint += (_, e) => DrawLayoutToggleIcon(e.Graphics, button.ClientRectangle, pointsRight);
        button.Click += (_, _) => toggleAction();
        return button;
    }

    private void SyncLayoutControlsVisibility()
    {
        var shown = _getShowLayoutSet();
        var expanded = shown && _getLayoutControlsExpanded();
        foreach (var control in _layoutControls)
        {
            control.Visible = expanded;
        }

        UpdateToolbarWidth();
        foreach (var toggleButton in new[] { _layoutLeftToggleButton, _layoutRightToggleButton })
        {
            if (toggleButton is null)
            {
                continue;
            }

            toggleButton.Visible = shown;
            _toolTip.SetToolTip(
                toggleButton,
                expanded ? "창 위치 저장 세트 숨기기" : "창 위치 저장 세트 펼치기");
            toggleButton.Invalidate();
        }
    }

    private void AddAnnotationControl(FlowLayoutPanel panel, Control control, string toolTip)
    {
        panel.Controls.Add(control);
        _annotationControls.Add(control);
        _toolTip.SetToolTip(control, toolTip);
    }

    private Button CreateCommandButton(string text, int width, Action action)
    {
        var button = CreateFlatButton(text, width);
        button.Click += (_, _) => action();
        return button;
    }

    private void SyncAnnotationControlsVisibility()
    {
        var shown = _getShowAnnotationSet();
        var expanded = shown && _getAnnotationControlsExpanded();
        foreach (var control in _annotationControls)
        {
            control.Visible = expanded;
        }

        UpdateToolbarWidth();
        foreach (var toggleButton in new[] { _annotationLeftToggleButton, _annotationRightToggleButton })
        {
            if (toggleButton is null)
            {
                continue;
            }

            toggleButton.Visible = shown;
            _toolTip.SetToolTip(toggleButton, expanded ? "마킹 도구 세트 숨기기" : "마킹 도구 세트 펼치기");
            toggleButton.Invalidate();
        }
    }

    private void UpdateToolbarWidth()
    {
        var showLayout = _getShowLayoutSet();
        var showAnnotation = _getShowAnnotationSet();
        var showProgram = _getShowProgramSet();
        Width = CoreWidth
            + (showLayout ? SetToggleAddition : 0)
            + (showAnnotation ? SetToggleAddition : 0)
            + (showProgram ? SetToggleAddition : 0)
            + (showLayout && _getLayoutControlsExpanded() ? LayoutExpandedAddition : 0)
            + (showAnnotation && _getAnnotationControlsExpanded() ? AnnotationExpandedAddition : 0)
            + (showProgram && _getProgramControlsExpanded() ? ProgramExpandedAddition : 0);
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
        foreach (var control in _programControls)
        {
            control.Visible = expanded;
        }

        UpdateToolbarWidth();
        foreach (var toggleButton in new[] { _programLeftToggleButton, _programRightToggleButton })
        {
            if (toggleButton is null)
            {
                continue;
            }

            toggleButton.Visible = shown;
            _toolTip.SetToolTip(toggleButton, expanded ? "프로그램 실행 세트 숨기기" : "프로그램 실행 세트 펼치기");
            toggleButton.Invalidate();
        }
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

    private void MarkerNumberTextChanged(object? sender, EventArgs e)
    {
        if (_updatingMarkerNumber || sender is not Control editor)
        {
            return;
        }

        if (int.TryParse(editor.Text.Trim(), out var number) && number is >= 1 and <= 9999)
        {
            _setNextMarkerNumber(number);
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
            if (button == _markerColorButton || button == _penColorButton)
            {
                continue;
            }

            button.BackColor = background;
            button.ForeColor = foreground;
            button.FlatAppearance.MouseOverBackColor = hover;
            button.FlatAppearance.MouseDownBackColor = pressed;
            if (button != _annotationLeftToggleButton && button != _annotationRightToggleButton &&
                button != _layoutLeftToggleButton && button != _layoutRightToggleButton &&
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

    private Button CreateMoveButton(WindowControlIcon icon, MoveDirection direction)
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
        _toolTip.SetToolTip(button, toolTip);
        return button;
    }

    private Button CreateHalfButton(WindowControlIcon icon, WindowHalf half)
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
        _toolTip.SetToolTip(button, toolTip);
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
        button.Paint += (_, e) => DrawWindowControlIcon(e.Graphics, button.ClientRectangle, icon, button.ForeColor);
        button.Click += (_, _) =>
        {
            if (!requiresTarget || (TargetWindow != IntPtr.Zero && WindowController.IsMovableWindow(TargetWindow)))
            {
                action();
            }
        };
        return button;
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
