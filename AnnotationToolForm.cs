using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace WindowForm_Move;

public sealed class AnnotationToolForm : Form
{
    private const int ToolButtonWidth = 92;
    private const int ToolButtonHeight = 76;
    private const int SettingControlWidth = ToolButtonWidth;
    private const uint MfByCommand = 0x00000000;
    private const uint MfEnabled = 0x00000000;
    private const uint MfGrayed = 0x00000001;
    private const uint ScClose = 0xF060;
    private static readonly IntPtr HwndTopMost = new(-1);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;

    private readonly AnnotationManager _manager;
    private readonly Action<AnnotationTool> _toggleTool;
    private readonly ToolTip _toolTip = new() { ShowAlways = true };
    private readonly Dictionary<AnnotationTool, Button> _toolButtons = new();
    private readonly Dictionary<string, Button> _configuredButtons = new();
    private readonly Dictionary<string, Action> _buttonActions = new();
    private readonly Dictionary<string, string> _compactNames = new();
    private readonly HashSet<string> _textlessButtons = new();
    private readonly TableLayoutPanel _root;
    private readonly GroupBox _markerGroup;
    private readonly GroupBox _drawingGroup;
    private readonly GroupBox _editGroup;
    private Panel? _targetPanel;
    private Panel? _editPanel;
    private FlowLayoutPanel? _editTools;
    private Button? _exitButton;
    private readonly Button _collapseButton;
    private readonly Button _markerColorButton;
    private readonly Button _penColorButton;
    private readonly NumericUpDown _markerSizeInput;
    private readonly NumericUpDown _markerNumberInput;
    private readonly NumericUpDown _penWidthInput;
    private readonly ComboBox _fontInput;
    private readonly NumericUpDown _fontSizeInput;
    private readonly ComboBox _targetInput;
    private readonly Label _statusLabel;
    private bool _isCollapsed;
    private bool _syncing;

    public AnnotationToolForm(
        AnnotationManager manager,
        Action<AnnotationTool> toggleTool,
        Action capture)
    {
        _manager = manager;
        _toggleTool = toggleTool;

        Text = "Smart_Window 마킹 도구";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(880, 628);
        Font = new Font("Segoe UI", 9F);

        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(247, 248, 250)
        };
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 102F));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 232F));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        Controls.Add(_root);

        _targetInput = new ComboBox
        {
            Dock = DockStyle.None,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(8, 8, 16, 8),
            Font = new Font("Segoe UI", 9F)
        };
        _targetInput.SelectedIndexChanged += (_, _) => SelectTarget();
        _statusLabel = new Label
        {
            Name = "StatusLabel",
            Dock = DockStyle.None,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(8),
            BackColor = Color.FromArgb(236, 245, 255),
            ForeColor = Color.FromArgb(20, 76, 140),
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5F)
        };
        _collapseButton = new Button
        {
            Name = "CollapseButton",
            Text = "접기",
            Dock = DockStyle.None,
            Margin = new Padding(8),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = _statusLabel.Font,
            TabStop = false
        };
        _collapseButton.FlatAppearance.BorderSize = 1;
        _collapseButton.FlatAppearance.BorderColor = Color.FromArgb(185, 185, 185);
        _collapseButton.Click += (_, _) => SetCollapsed(!_isCollapsed);
        _toolTip.SetToolTip(_collapseButton, "마킹 도구 창을 접거나 펼칩니다.");
        _root.Controls.Add(CreateTargetGroup(), 0, 0);

        _markerSizeInput = CreateNumber(manager.MarkerSize, 4, 60, 1);
        _markerSizeInput.ValueChanged += (_, _) =>
        {
            if (!_syncing)
            {
                _manager.SetMarkerSize((int)_markerSizeInput.Value);
            }
        };
        _markerNumberInput = CreateNumber(manager.NextMarkerNumber, 1, 9999, 1);
        _markerNumberInput.ValueChanged += (_, _) =>
        {
            if (!_syncing)
            {
                _manager.SetNextMarkerNumber((int)_markerNumberInput.Value);
            }
        };
        _markerColorButton = CreateColorButton(
            "marker.marker_color",
            manager.MarkerColor,
            manager.ChooseMarkerColor,
            "색상");
        _markerGroup = CreateMarkerGroup();
        _root.Controls.Add(_markerGroup, 0, 1);

        _penColorButton = CreateColorButton(
            "marker.pen_color",
            manager.PenColor,
            manager.ChoosePenColor,
            "선색");
        _penWidthInput = CreateNumber((decimal)manager.PenWidth, 1, 20, 1);
        _penWidthInput.ValueChanged += (_, _) =>
        {
            if (!_syncing)
            {
                _manager.SetPenWidth((float)_penWidthInput.Value);
            }
        };
        _fontInput = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(4),
            Width = SettingControlWidth
        };
        using (var fonts = new InstalledFontCollection())
        {
            _fontInput.Items.AddRange(fonts.Families
                .Select(family => family.Name)
                .OrderBy(name => name)
                .Cast<object>()
                .ToArray());
        }
        _fontInput.SelectedItem = manager.MemoFontName;
        if (_fontInput.SelectedIndex < 0)
        {
            _fontInput.Items.Insert(0, manager.MemoFontName);
            _fontInput.SelectedIndex = 0;
        }
        _fontInput.SelectedIndexChanged += (_, _) => ApplyFontSetting();
        _fontSizeInput = CreateNumber((decimal)manager.MemoFontSize, 8, 36, 1);
        _fontSizeInput.ValueChanged += (_, _) => ApplyFontSetting();
        _drawingGroup = CreateDrawingGroup();
        _root.Controls.Add(_drawingGroup, 0, 2);

        _editGroup = CreateEditGroup(capture);
        _root.Controls.Add(_editGroup, 0, 3);

        _manager.ToolbarStateChanged += SyncState;
        SyncState();
        LayoutFloatingRows();
    }

    public void ShowBelow(Rectangle anchorBounds)
    {
        var screen = Screen.FromRectangle(anchorBounds);
        var x = Math.Clamp(
            anchorBounds.Right - Width,
            screen.WorkingArea.Left,
            Math.Max(screen.WorkingArea.Left, screen.WorkingArea.Right - Width));
        var y = anchorBounds.Bottom + 4;
        if (y + Height > screen.WorkingArea.Bottom)
        {
            y = Math.Max(screen.WorkingArea.Top, anchorBounds.Top - Height - 4);
        }

        Location = new Point(x, y);
        _statusLabel.Text = "마킹 도구 준비";
        if (!Visible)
        {
            Show();
        }
        else
        {
            BringToFront();
        }
    }

    public void ShowForPresentation(Rectangle targetBounds)
    {
        EnsureVisibleOnTarget(targetBounds);
        if (!Visible)
        {
            Show();
        }
        BringToFront();
        SetWindowPos(
            Handle,
            HwndTopMost,
            0,
            0,
            0,
            0,
            SwpNoActivate | SwpNoMove | SwpNoSize);
        _statusLabel.Text = "마킹 중";
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

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && _manager.IsPresentationActive)
        {
            e.Cancel = true;
            System.Media.SystemSounds.Beep.Play();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _manager.ToolbarStateChanged -= SyncState;
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    private GroupBox CreateTargetGroup()
    {
        var group = CreateGroup("대상 / 상태");
        var layout = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 8)
        };
        _targetPanel = layout;
        var targetControlHeight = _targetInput.Height;
        _statusLabel.AutoSize = false;
        _statusLabel.Height = targetControlHeight;
        _statusLabel.MinimumSize = new Size(0, targetControlHeight);
        _statusLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _collapseButton.Height = targetControlHeight;
        _collapseButton.MinimumSize = new Size(62, targetControlHeight);
        _collapseButton.Width = 62;
        layout.Controls.Add(_targetInput);
        layout.Controls.Add(_statusLabel);
        layout.Controls.Add(_collapseButton);
        layout.Layout += (_, _) => LayoutTargetRow(layout);
        group.Controls.Add(layout);
        return group;
    }

    private GroupBox CreateMarkerGroup()
    {
        var group = CreateGroup("마크 / 번호");
        var layout = CreateToolsAndSettingsLayout(66F);
        var tools = CreateToolFlow();
        tools.Controls.Add(CreateToolButton("marker.dot", "marker_dot", AnnotationTool.Dot, "원형 마킹"));
        tools.Controls.Add(CreateToolButton("marker.square", "marker_직사각형", AnnotationTool.FilledSquare, "사각 마크"));
        tools.Controls.Add(CreateToolButton("marker.number", "marker_number", AnnotationTool.Marker, "번호 마크"));
        layout.Controls.Add(tools, 0, 0);

        var settings = CreateSettingsTable(3);
        AddSettingRow(settings, 0, "크기", _markerSizeInput);
        AddSettingRow(settings, 1, "색상", _markerColorButton);
        AddSettingRow(settings, 2, "번호", _markerNumberInput);
        layout.Controls.Add(settings, 1, 0);
        group.Controls.Add(layout);
        return group;
    }

    private GroupBox CreateDrawingGroup()
    {
        var group = CreateGroup("그리기 도구");
        var layout = CreateToolsAndSettingsLayout(66F);
        var tools = CreateToolFlow();
        tools.WrapContents = true;
        tools.Controls.Add(CreateToolButton("marker.text", "marker_textbox", AnnotationTool.Text, "텍스트"));
        tools.Controls.Add(CreateToolButton("marker.arrow_note", "arrow_note", AnnotationTool.Arrow, "메모 화살표"));
        tools.Controls.Add(CreateToolButton("marker.pencil", "pencil", AnnotationTool.Pencil, "연필"));
        tools.Controls.Add(CreateToolButton("marker.double_arrow", "marker_양방향_화살표", AnnotationTool.DoubleArrow, "양방향"));
        tools.Controls.Add(CreateToolButton("marker.rectangle", "marker_빈직사각형", AnnotationTool.Rectangle, "사각형"));
        tools.Controls.Add(CreateToolButton("marker.ellipse", "marker_원", AnnotationTool.Ellipse, "원"));
        tools.Controls.Add(CreateToolButton("marker.line", "marker_직선", AnnotationTool.Line, "직선"));
        tools.Controls.Add(CreateToolButton("marker.horizontal", "marker_가로", AnnotationTool.HorizontalLine, "가로선"));
        tools.Controls.Add(CreateToolButton("marker.vertical", "marker_세로", AnnotationTool.VerticalLine, "세로선"));
        layout.Controls.Add(tools, 0, 0);

        var settings = CreateSettingsTable(4);
        AddSettingRow(settings, 0, "선색", _penColorButton);
        AddSettingRow(settings, 1, "두께", _penWidthInput);
        AddSettingRow(settings, 2, "글꼴", _fontInput);
        AddSettingRow(settings, 3, "글자 크기", _fontSizeInput);
        layout.Controls.Add(settings, 1, 0);
        group.Controls.Add(layout);
        return group;
    }

    private GroupBox CreateEditGroup(Action capture)
    {
        var group = CreateGroup("편집 / 캡처");
        var layout = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 8)
        };
        _editPanel = layout;
        var tools = CreateToolFlow();
        _editTools = tools;
        _editTools.Dock = DockStyle.None;
        tools.Controls.Add(CreateToolButton("marker.move", "marker_moving", AnnotationTool.Moving, "선택/이동"));
        tools.Controls.Add(CreateCommandButton("marker.undo", "undo", _manager.UndoLast, "실행취소"));
        tools.Controls.Add(CreateToolButton("marker.erase", "eraser", AnnotationTool.Eraser, "지우기"));
        tools.Controls.Add(CreateCommandButton("marker.clear", "clear_all", _manager.ClearAll, "전체삭제"));
        tools.Controls.Add(CreateCommandButton("marker.capture", "capture", capture, "영역 캡처"));
        tools.Controls.Add(CreateCommandButton(
            "marker.open_folder",
            "marker_폴더열기",
            _manager.OpenCaptureFolder,
            "폴더열기"));
        var exitButton = CreateCommandButton(
            "marker.exit",
            "marker_exit",
            _manager.EndPresentation,
            "마킹 종료",
            new Size(ToolButtonWidth, ToolButtonHeight));
        exitButton.BackColor = Color.FromArgb(220, 38, 45);
        exitButton.ForeColor = Color.White;
        _exitButton = exitButton;
        layout.Controls.Add(tools);
        layout.Controls.Add(exitButton);
        layout.Layout += (_, _) => LayoutEditRow(layout);
        group.Controls.Add(layout);
        return group;
    }

    private void LayoutTargetRow(Panel panel)
    {
        if (_markerSizeInput is null || _markerSizeInput.Parent is null)
        {
            return;
        }

        const int gap = 12;
        var rightGuide = ToChildX(panel, AbsoluteRight(_markerSizeInput));
        var contentLeft = panel.Padding.Left;
        var y = Math.Max(panel.Padding.Top, (panel.ClientSize.Height - _targetInput.Height) / 2);
        var targetWidth = Math.Min(360, Math.Max(260, (rightGuide - contentLeft - _collapseButton.Width - gap * 3) / 2));

        _collapseButton.SetBounds(
            rightGuide - _collapseButton.Width,
            y,
            _collapseButton.Width,
            _targetInput.Height);

        var statusLeft = contentLeft + targetWidth + gap;
        var statusWidth = Math.Max(180, _collapseButton.Left - gap - statusLeft);
        _statusLabel.SetBounds(
            statusLeft,
            y,
            statusWidth,
            _targetInput.Height);

        _targetInput.SetBounds(
            contentLeft,
            y,
            targetWidth,
            _targetInput.Height);
    }

    private void LayoutFloatingRows()
    {
        if (_targetPanel is not null)
        {
            LayoutTargetRow(_targetPanel);
        }
        if (_editPanel is not null)
        {
            LayoutEditRow(_editPanel);
        }
    }

    private void LayoutEditRow(Panel panel)
    {
        if (_markerSizeInput is null || _markerSizeInput.Parent is null || _editTools is null || _exitButton is null)
        {
            return;
        }

        const int gap = 12;
        var rightGuide = ToChildX(panel, AbsoluteRight(_markerSizeInput));
        var top = panel.Padding.Top;
        var buttonTop = top + _exitButton.Margin.Top;
        _exitButton.SetBounds(
            rightGuide - _exitButton.Width,
            buttonTop,
            _exitButton.Width,
            ToolButtonHeight);
        _editTools.SetBounds(
            panel.Padding.Left,
            top,
            Math.Max(0, _exitButton.Left - gap - panel.Padding.Left),
            ToolButtonHeight + _exitButton.Margin.Vertical);
    }

    private static int AbsoluteRight(Control control)
    {
        return AbsoluteLeft(control) + control.Width;
    }

    private static int AbsoluteLeft(Control control)
    {
        var left = control.Left;
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            left += parent.Left;
        }
        return left;
    }

    private static int ToChildX(Control childParent, int formX)
    {
        return formX - AbsoluteLeft(childParent);
    }

    private static GroupBox CreateGroup(string title)
    {
        return new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            BackColor = Color.White
        };
    }

    private static TableLayoutPanel CreateToolsAndSettingsLayout(float toolsPercent)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12, 8, 12, 8)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, toolsPercent));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F - toolsPercent));
        return layout;
    }

    private static FlowLayoutPanel CreateToolFlow()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
    }

    private static TableLayoutPanel CreateSettingsTable(int rows)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 186,
            ColumnCount = 2,
            RowCount = rows,
            Padding = Padding.Empty,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var row = 0; row < rows; row++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
        }
        return table;
    }

    private static void AddSettingRow(TableLayoutPanel table, int row, string name, Control control)
    {
        table.Controls.Add(new Label
        {
            Text = name,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F)
        }, 0, row);
        control.Dock = DockStyle.None;
        control.Anchor = AnchorStyles.Right;
        control.Width = SettingControlWidth;
        control.Margin = new Padding(4);
        table.Controls.Add(control, 1, row);
    }

    private Button CreateColorButton(string id, Color color, Action action, string compactName)
    {
        var button = new Button
        {
            Text = string.Empty,
            BackColor = color,
            Size = new Size(SettingControlWidth, 30),
            FlatStyle = FlatStyle.Flat,
            TabStop = false
        };
        button.Click += (_, _) => action();
        _textlessButtons.Add(id);
        ConfigureButton(id, button, action, compactName);
        return button;
    }

    private Button CreateToolButton(
        string id,
        string assetName,
        AnnotationTool tool,
        string compactName)
    {
        var action = () => _toggleTool(tool);
        var button = CreateIconButton(id, assetName, action, compactName);
        _toolButtons[tool] = button;
        return button;
    }

    private Button CreateCommandButton(
        string id,
        string assetName,
        Action action,
        string compactName,
        Size? size = null)
    {
        return CreateIconButton(id, assetName, action, compactName, size);
    }

    private Button CreateIconButton(
        string id,
        string assetName,
        Action action,
        string compactName,
        Size? size = null)
    {
        var button = new Button
        {
            Text = _manager.GetButtonLabel(id, compactName),
            Size = size ?? new Size(ToolButtonWidth, ToolButtonHeight),
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(250, 250, 250),
            ForeColor = Color.Black,
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.BottomCenter,
            Padding = new Padding(2, 2, 2, 5),
            TabStop = false
        };
        button.Paint += (_, e) =>
        {
            var iconSize = Math.Min(34, button.Height - 32);
            var iconRect = new Rectangle(
                (button.Width - iconSize) / 2,
                7,
                iconSize,
                iconSize);
            IconAssets.Draw(
                e.Graphics,
                iconRect,
                assetName,
                button.ForeColor,
                _manager.SharpIconRendering);
        };
        button.Click += (_, _) => action();
        ConfigureButton(id, button, action, compactName);
        return button;
    }

    private void ConfigureButton(string id, Button button, Action action, string compactName)
    {
        _configuredButtons[id] = button;
        _buttonActions[id] = action;
        _compactNames[id] = compactName;
        _toolTip.SetToolTip(button, _manager.GetButtonName(id));
    }

    private static NumericUpDown CreateNumber(
        decimal value,
        decimal minimum,
        decimal maximum,
        decimal increment)
    {
        return new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Increment = increment,
            Value = Math.Clamp(value, minimum, maximum),
            Width = SettingControlWidth,
            TextAlign = HorizontalAlignment.Center
        };
    }

    private void ApplyFontSetting()
    {
        if (_syncing || _fontInput.SelectedItem is not string fontName)
        {
            return;
        }
        _manager.SetMemoFont(fontName, (float)_fontSizeInput.Value);
    }

    private void SelectTarget()
    {
        if (_syncing || _targetInput.SelectedItem is not AnnotationTarget target)
        {
            return;
        }
        if (!_manager.SelectTarget(target.Id))
        {
            SyncState();
        }
    }

    private void SyncState()
    {
        if (IsDisposed)
        {
            return;
        }

        _syncing = true;
        try
        {
            _markerColorButton.BackColor = _manager.MarkerColor;
            _penColorButton.BackColor = _manager.PenColor;
            _markerSizeInput.Value = Math.Clamp(
                _manager.MarkerSize,
                (int)_markerSizeInput.Minimum,
                (int)_markerSizeInput.Maximum);
            _markerNumberInput.Value = Math.Clamp(
                _manager.NextMarkerNumber,
                (int)_markerNumberInput.Minimum,
                (int)_markerNumberInput.Maximum);
            _penWidthInput.Value = Math.Clamp(
                (decimal)_manager.PenWidth,
                _penWidthInput.Minimum,
                _penWidthInput.Maximum);
            _fontSizeInput.Value = Math.Clamp(
                (decimal)_manager.MemoFontSize,
                _fontSizeInput.Minimum,
                _fontSizeInput.Maximum);

            var selectedTargetId = _manager.SelectedTargetId;
            _targetInput.BeginUpdate();
            _targetInput.Items.Clear();
            _targetInput.Items.AddRange(_manager.GetAnnotationTargets().Cast<object>().ToArray());
            _targetInput.SelectedItem = _targetInput.Items
                .Cast<AnnotationTarget>()
                .FirstOrDefault(target => target.Id == selectedTargetId);
            _targetInput.Enabled = !_manager.IsPresentationActive;
            _targetInput.EndUpdate();

            foreach (var pair in _toolButtons)
            {
                var active = pair.Key == _manager.ActiveTool;
                pair.Value.BackColor = active
                    ? Color.FromArgb(255, 235, 246)
                    : Color.FromArgb(250, 250, 250);
                pair.Value.FlatAppearance.BorderColor = active
                    ? Color.FromArgb(245, 35, 135)
                    : Color.FromArgb(185, 185, 185);
            }

            foreach (var pair in _configuredButtons)
            {
                pair.Value.Visible = _manager.GetButtonPreference(pair.Key).Visible;
                pair.Value.Text = _textlessButtons.Contains(pair.Key)
                    ? string.Empty
                    : _manager.GetButtonLabel(pair.Key, _compactNames[pair.Key]);
                _toolTip.SetToolTip(pair.Value, _manager.GetButtonName(pair.Key));
            }

            SetCloseButtonEnabled(!_manager.IsPresentationActive);
            _statusLabel.Text = !string.IsNullOrWhiteSpace(_manager.OperationStatus)
                ? $"최근 작업: {_manager.OperationStatus}"
                : _manager.IsPresentationActive
                    ? $"마킹 중: {_manager.SelectedTargetDisplayName}"
                    : $"준비: {_manager.SelectedTargetDisplayName}";
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SetCloseButtonEnabled(bool enabled)
    {
        if (!IsHandleCreated)
        {
            return;
        }
        var systemMenu = GetSystemMenu(Handle, false);
        if (systemMenu == IntPtr.Zero)
        {
            return;
        }
        EnableMenuItem(
            systemMenu,
            ScClose,
            MfByCommand | (enabled ? MfEnabled : MfGrayed));
        DrawMenuBar(Handle);
    }

    private void SetCollapsed(bool collapsed)
    {
        _isCollapsed = collapsed;
        _markerGroup.Visible = !collapsed;
        _drawingGroup.Visible = !collapsed;
        _editGroup.Visible = !collapsed;
        _root.RowStyles[1].Height = collapsed ? 0F : 150F;
        _root.RowStyles[2].Height = collapsed ? 0F : 232F;
        _root.RowStyles[3].SizeType = collapsed
            ? SizeType.Absolute
            : SizeType.Percent;
        _root.RowStyles[3].Height = collapsed ? 0F : 100F;
        ClientSize = collapsed
            ? new Size(ClientSize.Width, 126)
            : new Size(ClientSize.Width, 628);
        _collapseButton.Text = collapsed ? "펼치기" : "접기";
    }

    private void EnsureVisibleOnTarget(Rectangle targetBounds)
    {
        var screen = Screen.FromRectangle(targetBounds);
        var area = screen.WorkingArea;
        var x = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
        var y = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
        Location = new Point(x, y);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr windowHandle, bool revert);

    [DllImport("user32.dll")]
    private static extern uint EnableMenuItem(IntPtr menuHandle, uint menuItemId, uint enableFlags);

    [DllImport("user32.dll")]
    private static extern bool DrawMenuBar(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
