namespace WindowForm_Move;

public sealed class ArrowMemoForm : Form
{
    private readonly TextBox _memoInput = new();

    public ArrowMemoForm(string currentText)
    {
        Text = "화살표 메모";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(360, 180);
        Font = new Font("Segoe UI", 9F);

        var label = new Label
        {
            Text = "화살표 끝에 표시할 메모",
            AutoSize = true,
            Location = new Point(14, 14)
        };
        Controls.Add(label);

        _memoInput.Multiline = true;
        _memoInput.ScrollBars = ScrollBars.Vertical;
        _memoInput.Text = currentText;
        _memoInput.Location = new Point(14, 38);
        _memoInput.Size = new Size(332, 92);
        Controls.Add(_memoInput);

        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Size = new Size(76, 28) };
        var ok = new Button { Text = "확인", DialogResult = DialogResult.OK, Size = new Size(76, 28) };
        cancel.Location = new Point(188, 140);
        ok.Location = new Point(270, 140);
        Controls.Add(cancel);
        Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public string MemoText => _memoInput.Text.Trim();

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _memoInput.Focus();
        _memoInput.SelectAll();
    }
}
