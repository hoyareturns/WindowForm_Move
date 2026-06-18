namespace WindowForm_Move;

public sealed class OverlayForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    private readonly Func<bool> _getMoveAllWindows;
    private readonly Action _toggleMoveAllWindows;
    private readonly Func<bool> _getCrosshairEnabled;
    private readonly Action _toggleCrosshair;
    private readonly Func<IntPtr, string, bool> _searchRequested;
    private readonly Action _exitRequested;
    private readonly TextBox _searchBox = new();
    private Button? _allButton;
    private Button? _crosshairButton;

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
        Func<IntPtr, string, bool> searchRequested,
        Action exitRequested)
    {
        TargetWindow = targetWindow;
        _getMoveAllWindows = getMoveAllWindows;
        _toggleMoveAllWindows = toggleMoveAllWindows;
        _getCrosshairEnabled = getCrosshairEnabled;
        _toggleCrosshair = toggleCrosshair;
        _searchRequested = searchRequested;
        _exitRequested = exitRequested;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(28, 28, 28);
        Opacity = 1.0;
        Size = new Size(584, 28);
        Padding = new Padding(2);

        BuildButtons();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            var searchBounds = _searchBox.RectangleToScreen(_searchBox.ClientRectangle);
            if (!searchBounds.Contains(Cursor.Position))
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

        if (targetRect.Width < Width + 16 || targetRect.Height < Height + 8)
        {
            Hide();
            return false;
        }

        var x = targetRect.Right - Width - 4;
        var y = targetRect.Top + 5;

        if (x < targetRect.Left + 12)
        {
            x = targetRect.Left + 12;
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
        panel.Controls.Add(_crosshairButton);

        _searchBox.Size = new Size(140, 23);
        _searchBox.Margin = new Padding(1);
        _searchBox.BorderStyle = BorderStyle.FixedSingle;
        _searchBox.Font = new Font("Segoe UI", 9F);
        _searchBox.PlaceholderText = "Find";
        _searchBox.KeyPress += SearchBoxKeyPress;
        _searchBox.TextChanged += (_, _) => _searchBox.BackColor = Color.White;
        panel.Controls.Add(_searchBox);

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
        panel.Controls.Add(appExitButton);

        _allButton = CreateFlatButton("ALL", 34);
        _allButton.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _allButton.Click += (_, _) => _toggleMoveAllWindows();
        panel.Controls.Add(_allButton);

        var minimizeButton = CreateWindowIconButton(
            WindowControlIcon.Minimize,
            () => WindowController.ApplyAction(TargetWindow, WindowAction.Minimize));
        panel.Controls.Add(minimizeButton);

        var maximizeButton = CreateWindowIconButton(
            WindowControlIcon.Maximize,
            () => WindowController.ToggleMaximizeWindow(TargetWindow));
        panel.Controls.Add(maximizeButton);

        var closeButton = CreateWindowIconButton(
            WindowControlIcon.Close,
            () => WindowController.CloseWindow(TargetWindow));
        panel.Controls.Add(closeButton);

        Controls.Add(panel);
    }

    private Button CreateMoveButton(WindowControlIcon icon, MoveDirection direction)
    {
        var button = CreateWindowIconButton(icon, () => MoveTargets(direction), false);
        return button;
    }

    private Button CreateHalfButton(WindowControlIcon icon, WindowHalf half)
    {
        return CreateWindowIconButton(icon, () => WindowController.SnapWindowToHalf(TargetWindow, half), width: 24);
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
            case WindowControlIcon.HalfLeft:
            case WindowControlIcon.HalfRight:
            case WindowControlIcon.HalfTop:
            case WindowControlIcon.HalfBottom:
                DrawHalfIcon(graphics, bounds, pen, icon);
                break;
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
        Crosshair
    }

    private void SearchBoxKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar != (char)Keys.Return)
        {
            return;
        }

        e.Handled = true;
        BeginInvoke(new Action(SubmitSearch));
    }

    private void SubmitSearch()
    {
        if (string.IsNullOrWhiteSpace(_searchBox.Text))
        {
            return;
        }

        var succeeded = _searchRequested(TargetWindow, _searchBox.Text);
        _searchBox.BackColor = succeeded ? Color.White : Color.MistyRose;
        _searchBox.SelectAll();
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
