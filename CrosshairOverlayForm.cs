namespace WindowForm_Move;

public sealed class CrosshairOverlayForm : Form
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_LAYERED = 0x00080000;

    private readonly System.Windows.Forms.Timer _trackingTimer = new();

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED;
            return cp;
        }
    }

    public CrosshairOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Fuchsia;
        TransparencyKey = Color.Fuchsia;
        DoubleBuffered = true;

        _trackingTimer.Interval = 16;
        _trackingTimer.Tick += (_, _) => UpdateCrosshair();
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            UpdateCrosshair();
            if (!Visible)
            {
                Show();
            }

            _trackingTimer.Start();
        }
        else
        {
            _trackingTimer.Stop();
            Hide();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var cursor = PointToClient(Cursor.Position);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        using var pen = new Pen(Color.FromArgb(90, 200, 255), 1F);
        e.Graphics.DrawLine(pen, 0, cursor.Y, ClientSize.Width, cursor.Y);
        e.Graphics.DrawLine(pen, cursor.X, 0, cursor.X, ClientSize.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trackingTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateCrosshair()
    {
        var screenBounds = Screen.FromPoint(Cursor.Position).Bounds;
        if (Bounds != screenBounds)
        {
            Bounds = screenBounds;
        }

        Invalidate();
    }
}
