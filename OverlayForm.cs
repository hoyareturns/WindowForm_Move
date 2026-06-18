namespace WindowForm_Move;

public sealed class OverlayForm : Form
{
    private const int CollapsedWidth = 464;
    private const int ExpandedWidth = 632;
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
    private readonly Func<bool> _getLaunchMissingPrograms;
    private readonly Action _toggleLaunchMissingPrograms;
    private readonly Func<bool> _getLayoutControlsExpanded;
    private readonly Action _toggleLayoutControls;
    private readonly Action _exitRequested;
    private readonly ComboBox _layoutCombo = new();
    private readonly ToolTip _toolTip = new();
    private readonly List<Control> _layoutControls = new();
    private Button? _allButton;
    private Button? _crosshairButton;
    private Button? _launchMissingButton;
    private Button? _layoutToggleButton;

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
        Func<bool> getLaunchMissingPrograms,
        Action toggleLaunchMissingPrograms,
        Func<bool> getLayoutControlsExpanded,
        Action toggleLayoutControls,
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
        _getLaunchMissingPrograms = getLaunchMissingPrograms;
        _toggleLaunchMissingPrograms = toggleLaunchMissingPrograms;
        _getLayoutControlsExpanded = getLayoutControlsExpanded;
        _toggleLayoutControls = toggleLayoutControls;
        _exitRequested = exitRequested;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(28, 28, 28);
        Opacity = 1.0;
        Size = new Size(CollapsedWidth, 28);
        Padding = new Padding(2);

        _toolTip.InitialDelay = 350;
        _toolTip.ReshowDelay = 100;
        _toolTip.AutoPopDelay = 5000;

        BuildButtons();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            var comboBounds = _layoutCombo.RectangleToScreen(_layoutCombo.ClientRectangle);
            if (!comboBounds.Contains(Cursor.Position))
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

    public void SyncToggleStates()
    {
        if (_allButton is not null)
        {
            _allButton.BackColor = _getMoveAllWindows()
                ? Color.FromArgb(0, 105, 145)
                : Color.FromArgb(45, 45, 45);
            _allButton.Invalidate();
        }

        if (_crosshairButton is not null)
        {
            _crosshairButton.BackColor = _getCrosshairEnabled()
                ? Color.FromArgb(0, 105, 145)
                : Color.FromArgb(45, 45, 45);
            _crosshairButton.Invalidate();
        }

        if (_launchMissingButton is not null)
        {
            _launchMissingButton.BackColor = _getLaunchMissingPrograms()
                ? Color.FromArgb(0, 105, 145)
                : Color.FromArgb(45, 45, 45);
            _launchMissingButton.Invalidate();
        }

        SyncLayoutControlsVisibility();
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

        _layoutToggleButton = CreateLayoutToggleButton();
        panel.Controls.Add(_layoutToggleButton);

        _layoutCombo.Size = new Size(70, 23);
        _layoutCombo.DropDownWidth = 150;
        _layoutCombo.Margin = new Padding(1);
        _layoutCombo.DropDownStyle = ComboBoxStyle.DropDown;
        _layoutCombo.FlatStyle = FlatStyle.Flat;
        _layoutCombo.Font = new Font("Segoe UI", 8.5F);
        panel.Controls.Add(_layoutCombo);
        _layoutControls.Add(_layoutCombo);
        _toolTip.SetToolTip(_layoutCombo, "창 위치 이름 입력 또는 저장 목록 선택");

        AddLayoutControl(panel, CreateLayoutButton(WindowControlIcon.SaveLayout, "현재 창 위치 저장", _saveLayout));
        AddLayoutControl(panel, CreateLayoutButton(WindowControlIcon.LoadLayout, "선택한 창 위치 불러오기", _loadLayout));
        AddLayoutControl(panel, CreateLayoutButton(WindowControlIcon.DeleteLayout, "선택한 창 위치 삭제", _deleteLayout));

        _launchMissingButton = CreateFlatButton("RUN", 28);
        _launchMissingButton.Font = new Font("Segoe UI", 7F, FontStyle.Bold);
        _launchMissingButton.Click += (_, _) => _toggleLaunchMissingPrograms();
        _toolTip.SetToolTip(_launchMissingButton, "불러올 때 누락된 프로그램 자동 실행");
        panel.Controls.Add(_launchMissingButton);
        _layoutControls.Add(_launchMissingButton);

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
        SyncLayoutControlsVisibility();
    }

    private void AddLayoutControl(FlowLayoutPanel panel, Control control)
    {
        panel.Controls.Add(control);
        _layoutControls.Add(control);
    }

    private Button CreateLayoutToggleButton()
    {
        var button = CreateFlatButton(string.Empty, 22);
        button.Paint += (_, e) => DrawLayoutToggleIcon(e.Graphics, button.ClientRectangle, _getLayoutControlsExpanded());
        button.Click += (_, _) => _toggleLayoutControls();
        return button;
    }

    private void SyncLayoutControlsVisibility()
    {
        var expanded = _getLayoutControlsExpanded();
        foreach (var control in _layoutControls)
        {
            control.Visible = expanded;
        }

        Width = expanded ? ExpandedWidth : CollapsedWidth;
        if (_layoutToggleButton is not null)
        {
            _toolTip.SetToolTip(
                _layoutToggleButton,
                expanded ? "창 위치 저장 영역 숨기기" : "창 위치 저장 영역 펼치기");
            _layoutToggleButton.Invalidate();
        }
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

    private Button CreateLayoutButton(WindowControlIcon icon, string toolTip, Func<string, bool> action)
    {
        var button = CreateWindowIconButton(icon, () =>
        {
            var succeeded = action(_layoutCombo.Text);
            _layoutCombo.BackColor = succeeded ? Color.White : Color.MistyRose;
            WindowController.ActivateWindow(TargetWindow);
        }, false, 20);
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
        button.Paint += (_, e) => DrawWindowControlIcon(e.Graphics, button.ClientRectangle, icon);
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

    private static void DrawWindowControlIcon(Graphics graphics, Rectangle bounds, WindowControlIcon icon)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        using var pen = new Pen(Color.White, 2F)
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
                graphics.DrawRectangle(pen, 7, 6, bounds.Width - 15, 11);
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
            case WindowControlIcon.SaveLayout:
                graphics.DrawRectangle(pen, 5, 5, bounds.Width - 11, 13);
                graphics.DrawLine(pen, 8, 5, 8, 10);
                graphics.DrawLine(pen, 8, 10, bounds.Width - 8, 10);
                break;
            case WindowControlIcon.LoadLayout:
                graphics.DrawLine(pen, bounds.Width / 2, 5, bounds.Width / 2, 15);
                graphics.DrawLine(pen, bounds.Width / 2, 15, bounds.Width / 2 - 4, 11);
                graphics.DrawLine(pen, bounds.Width / 2, 15, bounds.Width / 2 + 4, 11);
                graphics.DrawLine(pen, 5, 18, bounds.Width - 5, 18);
                break;
            case WindowControlIcon.DeleteLayout:
                graphics.DrawRectangle(pen, 7, 8, bounds.Width - 15, 10);
                graphics.DrawLine(pen, 5, 6, bounds.Width - 5, 6);
                graphics.DrawLine(pen, 9, 4, bounds.Width - 9, 4);
                break;
            case WindowControlIcon.HalfLeft:
            case WindowControlIcon.HalfRight:
            case WindowControlIcon.HalfTop:
            case WindowControlIcon.HalfBottom:
                DrawHalfIcon(graphics, bounds, pen, icon);
                break;
        }
    }

    private static void DrawLayoutToggleIcon(Graphics graphics, Rectangle bounds, bool expanded)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        using var pen = new Pen(Color.White, 2F);
        var centerX = bounds.Width / 2;

        if (expanded)
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
        using var brush = new SolidBrush(Color.White);
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
        SaveLayout,
        LoadLayout,
        DeleteLayout
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
