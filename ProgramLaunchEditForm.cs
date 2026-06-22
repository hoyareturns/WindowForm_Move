namespace WindowForm_Move;

public sealed class ProgramLaunchEditForm : Form
{
    private readonly TextBox _nameInput = new();
    private readonly TextBox _targetPathInput = new();
    private readonly TextBox _launcherPathInput = new();
    private readonly TextBox _argumentsInput = new();
    private readonly TextBox _workingDirectoryInput = new();
    private bool _saveAsCopy;

    public ProgramLaunchEditForm(ProgramLaunchEntry entry)
    {
        Text = "실행 항목 편집";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(680, 286);
        Font = new Font("Segoe UI", 9F);

        _nameInput.Text = entry.Name;
        _targetPathInput.Text = entry.FilePath;
        _launcherPathInput.Text = entry.LauncherPath ?? string.Empty;
        _argumentsInput.Text = entry.Arguments;
        _workingDirectoryInput.Text = entry.WorkingDirectory;

        var table = new TableLayoutPanel
        {
            Location = new Point(12, 12),
            Size = new Size(656, 188),
            ColumnCount = 4,
            RowCount = 5
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        AddRow(table, 0, "표시 이름", _nameInput, null, null);
        AddRow(table, 1, "대상 파일/폴더", _targetPathInput, CreateTargetFileButton(), CreateTargetFolderButton());
        AddRow(table, 2, "실행 도구", _launcherPathInput, CreateLauncherButton(), null);
        AddRow(table, 3, "실행 인수", _argumentsInput, null, null);
        AddRow(table, 4, "작업 폴더", _workingDirectoryInput, CreateWorkingFolderButton(), null);
        Controls.Add(table);

        var help = new Label
        {
            Text = "실행 도구를 지정하면 인수의 {file} 위치에 대상 경로를 넣습니다. {file}이 없으면 대상 경로를 인수 끝에 추가합니다.",
            ForeColor = Color.DimGray,
            AutoSize = false,
            Size = new Size(560, 42),
            Location = new Point(108, 205)
        };
        Controls.Add(help);

        var copy = new Button { Text = "복사", Size = new Size(76, 28) };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Size = new Size(76, 28) };
        var ok = new Button { Text = "확인", Size = new Size(76, 28) };
        copy.Location = new Point(428, 250);
        cancel.Location = new Point(510, 250);
        ok.Location = new Point(592, 250);
        copy.Click += (_, _) => BeginCopy();
        ok.Click += (_, _) => Confirm();
        Controls.Add(copy);
        Controls.Add(cancel);
        Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public ProgramLaunchEntry CreateEntry()
    {
        var targetPath = CleanPath(_targetPathInput.Text);
        var workingDirectory = CleanPath(_workingDirectoryInput.Text);
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = Directory.Exists(targetPath)
                ? targetPath
                : Path.GetDirectoryName(targetPath) ?? string.Empty;
        }

        return new ProgramLaunchEntry(
            _nameInput.Text.Trim(),
            targetPath,
            workingDirectory,
            _argumentsInput.Text.Trim(),
            CleanPath(_launcherPathInput.Text));
    }

    public bool SaveAsCopy => _saveAsCopy;

    private void BeginCopy()
    {
        if (_saveAsCopy)
        {
            return;
        }

        _saveAsCopy = true;
        _nameInput.Text = $"{_nameInput.Text.Trim()} 복사본";
        Text = "실행 항목 복사본 편집";
        _argumentsInput.Focus();
        _argumentsInput.SelectAll();
    }

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(_nameInput.Text))
        {
            ShowPathError("목록에 표시할 이름을 입력해 주세요.", _nameInput);
            return;
        }

        var targetPath = CleanPath(_targetPathInput.Text);
        if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
        {
            ShowPathError("대상 파일 또는 폴더 경로를 확인해 주세요.", _targetPathInput);
            return;
        }

        var launcherPath = CleanPath(_launcherPathInput.Text);
        if (!string.IsNullOrWhiteSpace(launcherPath) && !File.Exists(launcherPath))
        {
            ShowPathError("별도 실행 도구 경로를 확인해 주세요.", _launcherPathInput);
            return;
        }

        var workingDirectory = CleanPath(_workingDirectoryInput.Text);
        if (!string.IsNullOrWhiteSpace(workingDirectory) && !Directory.Exists(workingDirectory))
        {
            ShowPathError("작업 폴더 경로를 확인해 주세요.", _workingDirectoryInput);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private Button CreateTargetFileButton()
    {
        return CreateBrowseButton("F", "대상 파일 선택", () =>
        {
            var path = SelectFile(_targetPathInput.Text, "대상 파일 선택");
            if (path is not null)
            {
                SetTargetPath(path);
            }
        });
    }

    private Button CreateTargetFolderButton()
    {
        return CreateBrowseButton("D", "대상 폴더 선택", () =>
        {
            var path = SelectFolder(_targetPathInput.Text, "대상 폴더 선택");
            if (path is not null)
            {
                SetTargetPath(path);
            }
        });
    }

    private Button CreateLauncherButton()
    {
        return CreateBrowseButton("...", "별도 실행 도구 선택", () =>
        {
            var path = SelectFile(_launcherPathInput.Text, "별도 실행 도구 선택");
            if (path is not null)
            {
                _launcherPathInput.Text = path;
            }
        });
    }

    private Button CreateWorkingFolderButton()
    {
        return CreateBrowseButton("...", "작업 폴더 선택", () =>
        {
            var path = SelectFolder(_workingDirectoryInput.Text, "작업 폴더 선택");
            if (path is not null)
            {
                _workingDirectoryInput.Text = path;
            }
        });
    }

    private void SetTargetPath(string path)
    {
        _targetPathInput.Text = path;
        if (string.IsNullOrWhiteSpace(_workingDirectoryInput.Text))
        {
            _workingDirectoryInput.Text = Directory.Exists(path)
                ? path
                : Path.GetDirectoryName(path) ?? string.Empty;
        }
    }

    private static Button CreateBrowseButton(string text, string accessibleName, Action action)
    {
        var button = new Button
        {
            Text = text,
            AccessibleName = accessibleName,
            Size = new Size(34, 23),
            Margin = Padding.Empty
        };
        button.Click += (_, _) => action();
        return button;
    }

    private string? SelectFile(string currentPath, string title)
    {
        using var dialog = new OpenFileDialog
        {
            Title = title,
            CheckFileExists = true,
            FileName = File.Exists(CleanPath(currentPath)) ? CleanPath(currentPath) : string.Empty,
            Filter = "모든 파일|*.*"
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }

    private string? SelectFolder(string currentPath, string description)
    {
        var cleanPath = CleanPath(currentPath);
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            SelectedPath = Directory.Exists(cleanPath) ? cleanPath : string.Empty
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private void ShowPathError(string message, Control control)
    {
        MessageBox.Show(this, message, "입력 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        control.Focus();
    }

    private static string CleanPath(string value)
    {
        return value.Trim().Trim('"');
    }

    private static void AddRow(
        TableLayoutPanel table,
        int row,
        string label,
        Control input,
        Control? firstButton,
        Control? secondButton)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(3, 4, 3, 4);
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        table.Controls.Add(input, 1, row);
        if (firstButton is not null)
        {
            firstButton.Anchor = AnchorStyles.Left;
            table.Controls.Add(firstButton, 2, row);
        }

        if (secondButton is not null)
        {
            secondButton.Anchor = AnchorStyles.Left;
            table.Controls.Add(secondButton, 3, row);
        }
    }
}
