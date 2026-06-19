namespace WindowForm_Move;

public sealed class AnnotationSettingsForm : Form
{
    private readonly Button _markerColorButton;
    private readonly NumericUpDown _markerSizeInput;
    private readonly Button _penColorButton;
    private readonly NumericUpDown _penWidthInput;

    public AnnotationSettingsForm(AnnotationSettings settings)
    {
        Text = "마킹 도구 설정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(310, 190);
        Font = new Font("Segoe UI", 9F);

        _markerColorButton = CreateColorButton(settings.MarkerColor);
        _markerSizeInput = CreateNumberInput(settings.MarkerSize, 18, 60, 1);
        _penColorButton = CreateColorButton(settings.PenColor);
        _penWidthInput = CreateNumberInput((decimal)settings.PenWidth, 1, 20, 1);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 130,
            Padding = new Padding(14),
            ColumnCount = 2,
            RowCount = 4
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        AddRow(table, 0, "마커 색상", _markerColorButton);
        AddRow(table, 1, "마커 크기", _markerSizeInput);
        AddRow(table, 2, "펜 색상", _penColorButton);
        AddRow(table, 3, "펜 두께", _penWidthInput);
        Controls.Add(table);

        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Size = new Size(76, 28) };
        var ok = new Button { Text = "확인", DialogResult = DialogResult.OK, Size = new Size(76, 28) };
        cancel.Location = new Point(138, 148);
        ok.Location = new Point(220, 148);
        Controls.Add(cancel);
        Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public void ApplyTo(AnnotationSettings settings)
    {
        settings.MarkerColorArgb = _markerColorButton.BackColor.ToArgb();
        settings.MarkerSize = (int)_markerSizeInput.Value;
        settings.PenColorArgb = _penColorButton.BackColor.ToArgb();
        settings.PenWidth = (float)_penWidthInput.Value;
    }

    private static Button CreateColorButton(Color color)
    {
        var button = new Button { BackColor = color, FlatStyle = FlatStyle.Flat, Size = new Size(70, 23) };
        button.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = button.BackColor, FullOpen = true };
            if (dialog.ShowDialog() == DialogResult.OK)
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

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        table.Controls.Add(control, 1, row);
    }
}
