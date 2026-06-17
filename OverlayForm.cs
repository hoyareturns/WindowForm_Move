namespace WindowForm_Move;

public sealed class OverlayForm : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TOPMOST = 0x00000008;

    private readonly CheckBox _allCheck = new();
    private readonly Func<bool> _getMoveAllWindows;
    private readonly Action<bool> _setMoveAllWindows;
    private readonly Action _exitRequested;

    public IntPtr TargetWindow { get; }

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

    public OverlayForm(IntPtr targetWindow, Func<bool> getMoveAllWindows, Action<bool> setMoveAllWindows, Action exitRequested)
    {
        TargetWindow = targetWindow;
        _getMoveAllWindows = getMoveAllWindows;
        _setMoveAllWindows = setMoveAllWindows;
        _exitRequested = exitRequested;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(28, 28, 28);
        Opacity = 0.86;
        Size = new Size(196, 28);
        Padding = new Padding(2);

        BuildButtons();
    }

    public bool UpdatePosition(bool buttonsVisible)
    {
        if (!buttonsVisible || WindowController.IsMinimized(TargetWindow) || !WindowController.IsMovableWindow(TargetWindow))
        {
            Hide();
            return false;
        }

        if (!WindowController.TryGetWindowRectangle(TargetWindow, out var targetRect))
        {
            Hide();
            return false;
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

        SyncAllCheck();
        return true;
    }

    public void SyncAllCheck()
    {
        if (_allCheck.Checked != _getMoveAllWindows())
        {
            _allCheck.CheckedChanged -= AllCheckChanged;
            _allCheck.Checked = _getMoveAllWindows();
            _allCheck.CheckedChanged += AllCheckChanged;
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
        _allCheck.CheckedChanged += AllCheckChanged;
        panel.Controls.Add(_allCheck);

        var closeButton = CreateFlatButton("x");
        closeButton.Click += (_, _) => _exitRequested();
        panel.Controls.Add(closeButton);

        Controls.Add(panel);
    }

    private void AllCheckChanged(object? sender, EventArgs e)
    {
        _setMoveAllWindows(_allCheck.Checked);
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
