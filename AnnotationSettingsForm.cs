namespace WindowForm_Move;

public sealed class AnnotationSettingsForm : Form
{
    private readonly NumericUpDown _markerSizeInput;
    private readonly Button _penColorButton;
    private readonly NumericUpDown _penWidthInput;
    private readonly TextBox _captureDirectoryInput;
    private readonly TextBox _fileNamePatternInput;

    public AnnotationSettingsForm(AnnotationSettings settings)
    {
        Text = "마킹 도구 설정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(430, 238);
        Font = new Font("Segoe UI", 9F);

        _markerSizeInput = CreateNumberInput(settings.MarkerSize, 18, 60, 1);
        _penColorButton = CreateColorButton(settings.PenColor);
        _penWidthInput = CreateNumberInput((decimal)settings.PenWidth, 1, 20, 1);
        _captureDirectoryInput = new TextBox { Text = settings.CaptureDirectory, Width = 205 };
        _fileNamePatternInput = new TextBox { Text = settings.CaptureFileNamePattern, Width = 225 };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 172,
            Padding = new Padding(14),
            ColumnCount = 2,
            RowCount = 5
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        AddRow(table, 0, "마커 크기", _markerSizeInput);
        AddRow(table, 1, "화살표 색상", _penColorButton);
        AddRow(table, 2, "화살표 두께", _penWidthInput);
        AddRow(table, 3, "캡처 저장 폴더", CreateDirectoryPicker());
        AddRow(table, 4, "파일명 규칙", _fileNamePatternInput);
        Controls.Add(table);

        var patternHelp = new Label
        {
            Text = "사용 가능: {date}  {time}  {datetime}",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(184, 179)
        };
        Controls.Add(patternHelp);

        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Size = new Size(76, 28) };
        var ok = new Button { Text = "확인", DialogResult = DialogResult.OK, Size = new Size(76, 28) };
        cancel.Location = new Point(266, 204);
        ok.Location = new Point(348, 204);
        Controls.Add(cancel);
        Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public void ApplyTo(AnnotationSettings settings)
    {
        settings.MarkerSize = (int)_markerSizeInput.Value;
        settings.PenColorArgb = _penColorButton.BackColor.ToArgb();
        settings.PenWidth = (float)_penWidthInput.Value;
        settings.CaptureDirectory = _captureDirectoryInput.Text.Trim();
        settings.CaptureFileNamePattern = _fileNamePatternInput.Text.Trim();
    }

    private static Button CreateColorButton(Color color)
    {
        var button = new Button { BackColor = color, FlatStyle = FlatStyle.Flat, Size = new Size(70, 23) };
        button.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = button.BackColor, FullOpen = true };
            var owner = button.FindForm();
            if (dialog.ShowDialog(owner) == DialogResult.OK)
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
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        var browse = new Button { Text = "...", Size = new Size(32, 23), Margin = new Padding(4, 0, 0, 0) };
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
        panel.Controls.Add(_captureDirectoryInput);
        panel.Controls.Add(browse);
        return panel;
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        table.Controls.Add(control, 1, row);
    }
}
