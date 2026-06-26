namespace WindowForm_Move;

public sealed class AnnotationSettingsForm : Form
{
    private readonly AnnotationSettings _settings;
    private readonly Func<bool>? _applySettings;
    private readonly NumericUpDown _markerSizeInput;
    private readonly Button _penColorButton;
    private readonly NumericUpDown _penWidthInput;
    private readonly TextBox _captureDirectoryInput;
    private readonly TextBox _fileNamePatternInput;
    private readonly CheckBox _showAnnotationSetInput;
    private readonly CheckBox _showLayoutSetInput;
    private readonly CheckBox _showProgramSetInput;
    private readonly CheckBox _expandAnnotationSetInput;
    private readonly CheckBox _expandLayoutSetInput;
    private readonly CheckBox _expandProgramSetInput;
    private readonly CheckBox _startToolbarExpandedInput;
    private readonly Button _toolbarColorButton;
    private readonly CheckBox _matchTargetColorInput;
    private readonly CheckBox _sharpIconInput;
    private readonly CheckBox _autoStartInput;
    private readonly DataGridView _buttonGrid;

    public AnnotationSettingsForm(
        AnnotationSettings settings,
        Func<bool>? applySettings = null)
    {
        _settings = settings;
        _applySettings = applySettings;

        Text = "Smart_Window 설정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(760, 560);
        Font = new Font("Segoe UI", 9F);

        _markerSizeInput = CreateNumberInput(settings.MarkerSize, 4, 60, 1);
        _penColorButton = CreateColorButton(settings.PenColor);
        _penWidthInput = CreateNumberInput((decimal)settings.PenWidth, 1, 20, 1);
        _captureDirectoryInput = new TextBox { Text = settings.CaptureDirectory, Width = 430 };
        _fileNamePatternInput = new TextBox { Text = settings.CaptureFileNamePattern, Width = 250 };
        _showAnnotationSetInput = CreateCheckBox("마킹 도구 세트", settings.ShowAnnotationSet);
        _showLayoutSetInput = CreateCheckBox("창 위치 저장 세트", settings.ShowLayoutSet);
        _showProgramSetInput = CreateCheckBox("프로그램 실행 세트", settings.ShowProgramSet);
        _expandAnnotationSetInput = CreateCheckBox("마킹 도구 시작 시 펼침", settings.ExpandAnnotationSetOnOpen);
        _expandLayoutSetInput = CreateCheckBox("창 위치 저장 시작 시 펼침", settings.ExpandLayoutSetOnOpen);
        _expandProgramSetInput = CreateCheckBox("프로그램 실행 세트 시작 시 펼침", settings.ExpandProgramSetOnOpen);
        _startToolbarExpandedInput = CreateCheckBox(
            "Smart_Window 실행 시 펼친 상태로 시작",
            settings.StartToolbarExpanded);
        _toolbarColorButton = CreateColorButton(settings.ToolbarColor);
        _matchTargetColorInput = CreateCheckBox(
            "대상 프로그램 제목 표시줄 색상에 맞춤",
            settings.MatchTargetWindowColor);
        _toolbarColorButton.Enabled = !_matchTargetColorInput.Checked;
        _matchTargetColorInput.CheckedChanged += (_, _) =>
            _toolbarColorButton.Enabled = !_matchTargetColorInput.Checked;
        _sharpIconInput = CreateCheckBox(
            "아이콘 선명도 우선 (해제 시 부드럽게 표시)",
            settings.SharpIconRendering);
        _autoStartInput = CreateCheckBox(
            "Windows 시작 시 Smart_Window 자동 실행",
            settings.AutoStartWithWindows);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        Controls.Add(root);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var generalPage = new TabPage("일반");
        var buttonsPage = new TabPage("버튼 설정");
        tabs.TabPages.Add(generalPage);
        tabs.TabPages.Add(buttonsPage);
        root.Controls.Add(tabs, 0, 0);

        BuildGeneralPage(generalPage);

        _buttonGrid = CreateButtonGrid(settings);
        var buttonPageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        buttonPageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        buttonPageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        buttonPageLayout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "단축키 예: Ctrl+Alt+L, Ctrl+Shift+F9. 필수 버튼은 숨길 수 없습니다.",
            Padding = new Padding(8, 9, 0, 0),
            ForeColor = Color.DimGray
        }, 0, 0);
        buttonPageLayout.Controls.Add(_buttonGrid, 0, 1);
        buttonsPage.Controls.Add(buttonPageLayout);

        root.Controls.Add(CreateCommandBar(), 0, 1);
    }

    public void ApplyTo(AnnotationSettings settings)
    {
        settings.MarkerSize = (int)_markerSizeInput.Value;
        settings.PenColorArgb = _penColorButton.BackColor.ToArgb();
        settings.PenWidth = (float)_penWidthInput.Value;
        settings.CaptureDirectory = _captureDirectoryInput.Text.Trim();
        settings.CaptureFileNamePattern = _fileNamePatternInput.Text.Trim();
        settings.ShowAnnotationSet = _showAnnotationSetInput.Checked;
        settings.ShowLayoutSet = _showLayoutSetInput.Checked;
        settings.ShowProgramSet = _showProgramSetInput.Checked;
        settings.ExpandAnnotationSetOnOpen = _expandAnnotationSetInput.Checked;
        settings.ExpandLayoutSetOnOpen = _expandLayoutSetInput.Checked;
        settings.ExpandProgramSetOnOpen = _expandProgramSetInput.Checked;
        settings.StartToolbarExpanded = _startToolbarExpandedInput.Checked;
        settings.ToolbarColorArgb = _toolbarColorButton.BackColor.ToArgb();
        settings.MatchTargetWindowColor = _matchTargetColorInput.Checked;
        settings.SharpIconRendering = _sharpIconInput.Checked;
        settings.AutoStartWithWindows = _autoStartInput.Checked;
        ApplyButtonGrid(settings);
    }

    private void BuildGeneralPage(TabPage page)
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 158F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        page.Controls.Add(content);

        var captureGroup = new GroupBox { Text = "캡처", Dock = DockStyle.Fill };
        var captureTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(10, 5, 10, 5)
        };
        captureTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        captureTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        AddRow(captureTable, 0, "캡처 저장 폴더", CreateDirectoryPicker());
        AddRow(captureTable, 1, "파일명 규칙", _fileNamePatternInput);
        captureTable.Controls.Add(new Label
        {
            Text = "사용 가능: {date}  {time}  {datetime}",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Anchor = AnchorStyles.Left
        }, 1, 2);
        captureGroup.Controls.Add(captureTable);
        content.Controls.Add(captureGroup, 0, 0);

        var appearanceGroup = new GroupBox { Text = "툴바 색상", Dock = DockStyle.Fill };
        var appearancePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 10, 0, 0),
            WrapContents = true
        };
        appearancePanel.Controls.Add(_toolbarColorButton);
        appearancePanel.Controls.Add(_matchTargetColorInput);
        appearancePanel.SetFlowBreak(_matchTargetColorInput, true);
        _sharpIconInput.Margin = new Padding(84, 4, 0, 0);
        appearancePanel.Controls.Add(_sharpIconInput);
        appearanceGroup.Controls.Add(appearancePanel);
        content.Controls.Add(appearanceGroup, 0, 1);

        var setGroup = new GroupBox { Text = "툴바 세트 표시", Dock = DockStyle.Fill };
        var setTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10, 8, 10, 8)
        };
        setTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
        setTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
        for (var row = 0; row < 4; row++)
        {
            setTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 29F));
        }
        setTable.Controls.Add(_showAnnotationSetInput, 0, 0);
        setTable.Controls.Add(_expandAnnotationSetInput, 1, 0);
        setTable.Controls.Add(_showLayoutSetInput, 0, 1);
        setTable.Controls.Add(_expandLayoutSetInput, 1, 1);
        setTable.Controls.Add(_showProgramSetInput, 0, 2);
        setTable.Controls.Add(_expandProgramSetInput, 1, 2);
        setTable.Controls.Add(_startToolbarExpandedInput, 0, 3);
        setTable.SetColumnSpan(_startToolbarExpandedInput, 2);
        setGroup.Controls.Add(setTable);
        content.Controls.Add(setGroup, 0, 2);

        var startupGroup = new GroupBox { Text = "Windows 시작", Dock = DockStyle.Fill };
        _autoStartInput.Location = new Point(14, 24);
        startupGroup.Controls.Add(_autoStartInput);
        content.Controls.Add(startupGroup, 0, 3);
    }

    private Control CreateCommandBar()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var apply = new Button { Text = "적용", Size = new Size(76, 28) };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Size = new Size(76, 28) };
        var ok = new Button { Text = "확인", Size = new Size(76, 28) };
        apply.Click += (_, _) => TryApply(closeAfterApply: false);
        ok.Click += (_, _) => TryApply(closeAfterApply: true);
        panel.Controls.Add(apply);
        panel.Controls.Add(cancel);
        panel.Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;
        return panel;
    }

    private bool TryApply(bool closeAfterApply)
    {
        if (!ValidateButtonSettings())
        {
            return false;
        }

        ApplyTo(_settings);
        if (_applySettings is not null && !_applySettings())
        {
            return false;
        }

        if (closeAfterApply)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
        return true;
    }

    private static CheckBox CreateCheckBox(string text, bool value)
    {
        return new CheckBox
        {
            Text = text,
            Checked = value,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
    }

    private static Button CreateColorButton(Color color)
    {
        var button = new Button { BackColor = color, FlatStyle = FlatStyle.Flat, Size = new Size(70, 23) };
        button.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = button.BackColor, FullOpen = true };
            if (dialog.ShowDialog(button.FindForm()) == DialogResult.OK)
            {
                button.BackColor = dialog.Color;
            }
        };
        return button;
    }

    private static NumericUpDown CreateNumberInput(decimal value, decimal min, decimal max, decimal increment)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Increment = increment,
            Value = Math.Clamp(value, min, max),
            Width = 70
        };
    }

    private Control CreateDirectoryPicker()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38F));
        _captureDirectoryInput.Dock = DockStyle.Fill;
        var browse = new Button { Text = "...", Dock = DockStyle.Fill, Margin = new Padding(4, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "캡처 저장 폴더 선택",
                SelectedPath = Directory.Exists(_captureDirectoryInput.Text)
                    ? _captureDirectoryInput.Text
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _captureDirectoryInput.Text = dialog.SelectedPath;
            }
        };
        panel.Controls.Add(_captureDirectoryInput, 0, 0);
        panel.Controls.Add(browse, 1, 0);
        return panel;
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, row);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        table.Controls.Add(control, 1, row);
    }

    private static DataGridView CreateButtonGrid(AnnotationSettings settings)
    {
        settings.EnsureButtonPreferences();
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            MultiSelect = false,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.None
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Group", HeaderText = "그룹", ReadOnly = true, Width = 105 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DefaultName", HeaderText = "기본 이름", ReadOnly = true, Width = 190 });
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Visible", HeaderText = "표시", Width = 52 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "표시 이름", Width = 190 });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Shortcut",
            HeaderText = "전역 단축키",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 120
        });

        foreach (var definition in ButtonCatalog.All)
        {
            var preference = settings.GetButtonPreference(definition.Id);
            var index = grid.Rows.Add(
                definition.Group,
                definition.DefaultName,
                definition.Required || preference.Visible,
                preference.DisplayName,
                preference.Shortcut);
            var row = grid.Rows[index];
            row.Tag = definition;
            if (definition.Required)
            {
                row.Cells["Visible"].ReadOnly = true;
                row.Cells["Visible"].Style.BackColor = SystemColors.Control;
            }
        }
        grid.KeyDown += (_, e) => CaptureShortcut(grid, e);
        grid.EditingControlShowing += (_, e) =>
        {
            if (e.Control is not TextBox editor)
            {
                return;
            }
            editor.KeyDown -= ShortcutEditorKeyDown;
            editor.KeyDown += ShortcutEditorKeyDown;
        };
        return grid;
    }

    private static void ShortcutEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox editor ||
            editor.FindForm() is not AnnotationSettingsForm form)
        {
            return;
        }
        CaptureShortcut(form._buttonGrid, e);
    }

    private static void CaptureShortcut(DataGridView grid, KeyEventArgs e)
    {
        if (grid.CurrentCell?.OwningColumn.Name != "Shortcut")
        {
            return;
        }

        if (HotkeyCapture.IsClearKey(e.KeyData))
        {
            grid.CurrentCell.Value = string.Empty;
            e.SuppressKeyPress = true;
            e.Handled = true;
            return;
        }

        if (!HotkeyCapture.TryFormat(e.KeyData, out var shortcut))
        {
            return;
        }

        grid.CurrentCell.Value = shortcut;
        e.SuppressKeyPress = true;
        e.Handled = true;
    }

    private bool ValidateButtonSettings()
    {
        var preferences = ReadButtonGrid();
        var result = ButtonSettingsValidator.Validate(preferences);
        foreach (DataGridViewRow row in _buttonGrid.Rows)
        {
            if (row.Tag is not ButtonDefinition definition)
            {
                continue;
            }
            row.DefaultCellStyle.BackColor = result.ConflictingButtonIds.Contains(definition.Id)
                ? Color.MistyRose
                : Color.White;
        }

        if (result.Succeeded)
        {
            return true;
        }
        MessageBox.Show(this, result.ErrorMessage, "버튼 설정 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private Dictionary<string, ButtonPreference> ReadButtonGrid()
    {
        _buttonGrid.EndEdit();
        var preferences = new Dictionary<string, ButtonPreference>();
        foreach (DataGridViewRow row in _buttonGrid.Rows)
        {
            if (row.Tag is not ButtonDefinition definition)
            {
                continue;
            }
            preferences[definition.Id] = new ButtonPreference
            {
                Visible = definition.Required || Convert.ToBoolean(row.Cells["Visible"].Value),
                DisplayName = Convert.ToString(row.Cells["DisplayName"].Value)?.Trim() ?? string.Empty,
                Shortcut = Convert.ToString(row.Cells["Shortcut"].Value)?.Trim() ?? string.Empty
            };
        }
        return preferences;
    }

    private void ApplyButtonGrid(AnnotationSettings settings)
    {
        var preferences = ReadButtonGrid();
        ButtonSettingsValidator.Validate(preferences);
        settings.ButtonPreferences = preferences;
        settings.EnsureButtonPreferences();
    }
}
