namespace WindowForm_Move;

public sealed class CaptureSelectionForm : Form
{
    private readonly Bitmap _screenImage;
    private Point _start;
    private Rectangle _selection;
    private bool _dragging;

    public CaptureSelectionForm(Bitmap screenImage, Rectangle virtualBounds)
    {
        _screenImage = screenImage;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Bounds = virtualBounds;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;
    }

    public Rectangle SelectedRegion => _selection;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawImageUnscaled(_screenImage, Point.Empty);
        using var shade = new SolidBrush(Color.FromArgb(105, Color.Black));
        e.Graphics.FillRectangle(shade, ClientRectangle);

        if (_selection.Width <= 0 || _selection.Height <= 0)
        {
            return;
        }

        e.Graphics.DrawImage(_screenImage, _selection, _selection, GraphicsUnit.Pixel);
        using var border = new Pen(Color.FromArgb(90, 205, 255), 2F);
        e.Graphics.DrawRectangle(border, Rectangle.Inflate(_selection, -1, -1));

        var label = $"{_selection.Width} x {_selection.Height}";
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
}
