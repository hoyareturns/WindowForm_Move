using System.Runtime.InteropServices;

namespace WindowForm_Move;

public sealed class CaptureSelectionForm : Form
{
    private readonly Bitmap _screenImage;
    private readonly Rectangle _virtualBounds;
    private Point _start;
    private Rectangle _selection;
    private bool _dragging;

    public CaptureSelectionForm(Bitmap screenImage, Rectangle virtualBounds)
    {
        _screenImage = screenImage;
        _virtualBounds = virtualBounds;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        ShowInTaskbar = false;
        TopMost = true;
        Bounds = virtualBounds;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;
    }

    public Rectangle SelectedRegion => MapClientToImage(_selection);
    public Rectangle SelectedScreenRegion => new(
        Left + _selection.Left,
        Top + _selection.Top,
        _selection.Width,
        _selection.Height);

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        SetWindowPos(
            Handle,
            HwndTopMost,
            _virtualBounds.Left,
            _virtualBounds.Top,
            _virtualBounds.Width,
            _virtualBounds.Height,
            SwpShowWindow);
        Activate();
        Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(_screenImage, ClientRectangle);
        using var shade = new SolidBrush(Color.FromArgb(105, Color.Black));
        e.Graphics.FillRectangle(shade, ClientRectangle);

        if (_selection.Width <= 0 || _selection.Height <= 0)
        {
            return;
        }

        var imageSelection = MapClientToImage(_selection);
        e.Graphics.DrawImage(
            _screenImage,
            _selection,
            imageSelection,
            GraphicsUnit.Pixel);
        using var border = new Pen(Color.FromArgb(90, 205, 255), 2F);
        e.Graphics.DrawRectangle(border, Rectangle.Inflate(_selection, -1, -1));

        var label = $"{imageSelection.Width} x {imageSelection.Height}";
        using var font = new Font("Segoe UI", 9F, FontStyle.Bold);
        var labelSize = e.Graphics.MeasureString(label, font);
        var labelRect = new RectangleF(
            _selection.Left,
            Math.Max(0, _selection.Top - labelSize.Height - 5),
            labelSize.Width + 10,
            labelSize.Height + 4);
        using var labelBack = new SolidBrush(Color.FromArgb(220, 28, 28, 28));
        using var labelText = new SolidBrush(Color.White);
        e.Graphics.FillRectangle(labelBack, labelRect);
        e.Graphics.DrawString(label, font, labelText, labelRect.Left + 5, labelRect.Top + 2);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _start = e.Location;
        _selection = Rectangle.Empty;
        _dragging = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
        {
            return;
        }

        _selection = NormalizeRectangle(_start, e.Location);
        _selection.Intersect(ClientRectangle);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging || e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = false;
        if (_selection.Width >= 5 && _selection.Height >= 5)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _screenImage.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Rectangle NormalizeRectangle(Point first, Point second)
    {
        return Rectangle.FromLTRB(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Max(first.X, second.X),
            Math.Max(first.Y, second.Y));
    }

    private Rectangle MapClientToImage(Rectangle clientRegion)
    {
        if (clientRegion.Width <= 0 ||
            clientRegion.Height <= 0 ||
            ClientSize.Width <= 0 ||
            ClientSize.Height <= 0)
        {
            return Rectangle.Empty;
        }

        var scaleX = _screenImage.Width / (double)ClientSize.Width;
        var scaleY = _screenImage.Height / (double)ClientSize.Height;
        var left = (int)Math.Floor(clientRegion.Left * scaleX);
        var top = (int)Math.Floor(clientRegion.Top * scaleY);
        var right = (int)Math.Ceiling(clientRegion.Right * scaleX);
        var bottom = (int)Math.Ceiling(clientRegion.Bottom * scaleY);
        var imageRegion = Rectangle.FromLTRB(left, top, right, bottom);
        imageRegion.Intersect(new Rectangle(Point.Empty, _screenImage.Size));
        return imageRegion;
    }

    private static readonly IntPtr HwndTopMost = new(-1);
    private const uint SwpShowWindow = 0x0040;

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
