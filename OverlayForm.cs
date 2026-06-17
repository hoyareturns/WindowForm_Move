namespace WindowForm_Move;

public sealed class OverlayForm : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TOPMOST = 0x00000008;

    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly CheckBox _allCheck = new();

    private IntPtr _targetWindow = IntPtr.Zero;
    private bool _buttonsVisible = true;

    public event EventHandler? ExitRequested;

    public bool MoveAllWindows
    {
        get => _allCheck.Checked;
        set => _allCheck.Checked = value;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            return cp;
        }
    }

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(28, 28, 28);
        Opacity = 0.86;
        Size = new Size(196, 28);
        Padding = new Padding(2);

        BuildButtons();

        _timer.Interval = 120;
        _timer.Tick += (_, _) => FollowForegroundWindow();
        _timer.Start();
    }

    public void ToggleVisible()
    {
        _buttonsVisible = !_buttonsVisible;
        Visible = _buttonsVisible;
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

        panel.Controls.Add(CreateMoveButton("<", MoveDirection.Left));
        panel.Controls.Add(CreateMoveButton(">", MoveDirection.Right));
        panel.Controls.Add(CreateMoveButton("^", MoveDirection.Up));
        panel.Controls.Add(CreateMoveButton("v", MoveDirection.Down));

        _allCheck.Text = "ALL";
        _allCheck.ForeColor = Color.White;
        _allCheck.AutoSize = false;
        _allCheck.Size = new Size(46, 23);
        _allCheck.TextAlign = ContentAlignment.MiddleCenter;
        _allCheck.Margin = new Padding(2, 1, 0, 0);
        panel.Controls.Add(_allCheck);

        var closeButton = CreateFlatButton("x");
        closeButton.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        panel.Controls.Add(closeButton);

        Controls.Add(panel);
    }

    private Button CreateMoveButton(string text, MoveDirection direction)
    {
        var button = CreateFlatButton(text);
        button.Click += (_, _) => MoveTargets(direction);
        return button;
    }

    private static Button CreateFlatButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(26, 23),
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

    private void FollowForegroundWindow()
    {
        var foreground = WindowController.GetForegroundWindow();
        if (foreground != IntPtr.Zero && !WindowController.BelongsToCurrentProcess(foreground))
        {
            _targetWindow = foreground;
        }

        if (!_buttonsVisible || _targetWindow == IntPtr.Zero || !WindowController.IsMovableWindow(_targetWindow))
        {
            Hide();
            return;
        }

        if (!WindowController.TryGetWindowRectangle(_targetWindow, out var targetRect))
        {
            Hide();
            return;
        }

        var x = targetRect.Right - Width - 170;
        var y = targetRect.Top + 5;

        if (x < targetRect.Left + 12)
        {
            x = targetRect.Left + 12;
        }

        SetBounds(x, y, Width, Height);

        if (!Visible)
        {
            Show();
        }
    }

    private void MoveTargets(MoveDirection direction)
    {
        var targets = MoveAllWindows
            ? WindowController.GetMovableWindows().Select(window => window.Handle).ToList()
            : GetSingleTarget();

        foreach (var target in targets)
        {
            WindowController.MoveWindowToDirection(target, direction);
        }
    }

    private IReadOnlyList<IntPtr> GetSingleTarget()
    {
        return _targetWindow != IntPtr.Zero && WindowController.IsMovableWindow(_targetWindow)
            ? new[] { _targetWindow }
            : Array.Empty<IntPtr>();
    }
}
