using System.Diagnostics;

namespace WindowForm_Move;

public partial class Form1 : Form
{
    private readonly ListBox _windowList = new();
    private readonly CheckBox _moveAllCheck = new();
    private readonly CheckBox _topMostCheck = new();
    private readonly Label _statusLabel = new();
    private readonly System.Windows.Forms.Timer _foregroundTimer = new();

    private IntPtr _lastExternalForeground = IntPtr.Zero;

    public Form1()
    {
        InitializeComponent();
        BuildUi();
        RefreshWindows();

        _foregroundTimer.Interval = 250;
        _foregroundTimer.Tick += (_, _) => TrackForegroundWindow();
        _foregroundTimer.Start();
    }

    private void BuildUi()
    {
        Text = "Window Move";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(440, 560);
        MinimumSize = new Size(390, 480);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            Text = "실행 중인 창 이동",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        root.Controls.Add(title, 0, 0);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        _moveAllCheck.Text = "모든 창 일괄 적용";
        _moveAllCheck.AutoSize = true;
        _moveAllCheck.Margin = new Padding(0, 4, 18, 4);
        _topMostCheck.Text = "항상 위";
        _topMostCheck.AutoSize = true;
        _topMostCheck.Margin = new Padding(0, 4, 18, 4);
        _topMostCheck.CheckedChanged += (_, _) => TopMost = _topMostCheck.Checked;
        var refreshButton = CreateButton("새로고침", (_, _) => RefreshWindows());
        refreshButton.Width = 86;
        options.Controls.Add(_moveAllCheck);
        options.Controls.Add(_topMostCheck);
        options.Controls.Add(refreshButton);
        root.Controls.Add(options, 0, 1);

        var directionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Margin = new Padding(0, 0, 0, 10)
        };
        for (var i = 0; i < 3; i++)
        {
            directionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            directionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        }

        directionPanel.Controls.Add(CreateDirectionButton("위", MoveDirection.Up), 1, 0);
        directionPanel.Controls.Add(CreateDirectionButton("좌", MoveDirection.Left), 0, 1);
        directionPanel.Controls.Add(CreateDirectionButton("우", MoveDirection.Right), 2, 1);
        directionPanel.Controls.Add(CreateDirectionButton("아래", MoveDirection.Down), 1, 2);
        root.Controls.Add(directionPanel, 0, 2);

        _windowList.Dock = DockStyle.Fill;
        _windowList.DisplayMember = nameof(WindowInfo.DisplayName);
        _windowList.IntegralHeight = false;
        root.Controls.Add(_windowList, 0, 3);

        var windowActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 10, 0, 8)
        };
        windowActions.Controls.Add(CreateButton("최소화", (_, _) => ApplyToTargets(WindowAction.Minimize)));
        windowActions.Controls.Add(CreateButton("최대화", (_, _) => ApplyToTargets(WindowAction.Maximize)));
        windowActions.Controls.Add(CreateButton("복원", (_, _) => ApplyToTargets(WindowAction.Restore)));
        windowActions.Controls.Add(CreateButton("창닫기", (_, _) => CloseTargets()));
        root.Controls.Add(windowActions, 0, 4);

        _statusLabel.AutoSize = false;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Height = 36;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Text = "목록에서 창을 고르거나, 다른 창을 클릭한 뒤 방향 버튼을 누르세요.";
        root.Controls.Add(_statusLabel, 0, 5);
    }

    private Button CreateDirectionButton(string text, MoveDirection direction)
    {
        var button = CreateButton(text, (_, _) => MoveTargets(direction));
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(5);
        button.Font = new Font(Font.FontFamily, 13, FontStyle.Bold);
        return button;
    }

    private static Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            Width = 78,
            Height = 34,
            Margin = new Padding(4),
            UseVisualStyleBackColor = true
        };
        button.Click += onClick;
        return button;
    }

    private void TrackForegroundWindow()
    {
        var foreground = WindowController.GetForegroundWindow();
        if (foreground == IntPtr.Zero || WindowController.BelongsToCurrentProcess(foreground))
        {
            return;
        }

        _lastExternalForeground = foreground;
    }

    private void RefreshWindows()
    {
        var windows = WindowController.GetMovableWindows()
            .OrderBy(window => window.ProcessName)
            .ThenBy(window => window.Title)
            .ToList();

        var selectedHandle = (_windowList.SelectedItem as WindowInfo)?.Handle;
        _windowList.BeginUpdate();
        _windowList.Items.Clear();
        foreach (var window in windows)
        {
            _windowList.Items.Add(window);
        }
        _windowList.EndUpdate();

        if (selectedHandle is not null)
        {
            for (var i = 0; i < _windowList.Items.Count; i++)
            {
                if (((WindowInfo)_windowList.Items[i]).Handle == selectedHandle)
                {
                    _windowList.SelectedIndex = i;
                    break;
                }
            }
        }

        _statusLabel.Text = $"{windows.Count}개 창을 찾았습니다.";
    }

    private IReadOnlyList<IntPtr> GetTargets()
    {
        if (_moveAllCheck.Checked)
        {
            return WindowController.GetMovableWindows().Select(window => window.Handle).ToList();
        }

        if (_windowList.SelectedItem is WindowInfo selected && WindowController.IsMovableWindow(selected.Handle))
        {
            return new[] { selected.Handle };
        }

        if (_lastExternalForeground != IntPtr.Zero && WindowController.IsMovableWindow(_lastExternalForeground))
        {
            return new[] { _lastExternalForeground };
        }

        return Array.Empty<IntPtr>();
    }

    private void MoveTargets(MoveDirection direction)
    {
        var targets = GetTargets();
        if (targets.Count == 0)
        {
            _statusLabel.Text = "이동할 창을 찾지 못했습니다.";
            return;
        }

        var moved = 0;
        foreach (var target in targets)
        {
            if (WindowController.MoveWindowToDirection(target, direction))
            {
                moved++;
            }
        }

        _statusLabel.Text = $"{moved}개 창을 {DirectionName(direction)}쪽으로 이동했습니다.";
        RefreshWindows();
    }

    private void ApplyToTargets(WindowAction action)
    {
        var targets = GetTargets();
        if (targets.Count == 0)
        {
            _statusLabel.Text = "적용할 창을 찾지 못했습니다.";
            return;
        }

        foreach (var target in targets)
        {
            WindowController.ApplyAction(target, action);
        }

        _statusLabel.Text = $"{targets.Count}개 창에 '{ActionName(action)}'을 적용했습니다.";
        RefreshWindows();
    }

    private void CloseTargets()
    {
        var targets = GetTargets();
        if (targets.Count == 0)
        {
            _statusLabel.Text = "닫을 창을 찾지 못했습니다.";
            return;
        }

        if (_moveAllCheck.Checked)
        {
            var result = MessageBox.Show(
                $"{targets.Count}개 창에 닫기 요청을 보낼까요?",
                "모든 창 닫기 확인",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                _statusLabel.Text = "창닫기를 취소했습니다.";
                return;
            }
        }

        foreach (var target in targets)
        {
            WindowController.CloseWindow(target);
        }

        _statusLabel.Text = $"{targets.Count}개 창에 닫기 요청을 보냈습니다.";
        RefreshWindows();
    }

    private static string DirectionName(MoveDirection direction) => direction switch
    {
        MoveDirection.Left => "왼쪽",
        MoveDirection.Right => "오른쪽",
        MoveDirection.Up => "위",
        MoveDirection.Down => "아래",
        _ => "해당"
    };

    private static string ActionName(WindowAction action) => action switch
    {
        WindowAction.Minimize => "최소화",
        WindowAction.Maximize => "최대화",
        WindowAction.Restore => "복원",
        _ => "작업"
    };
}
