namespace WindowForm_Move;

public static class AnnotationGeometry
{
    public const int MemoWidth = 180;
    public const int MemoHeight = 64;

    public static Rectangle GetMemoBounds(AnnotationArrow arrow, Rectangle virtualBounds)
    {
        var x = arrow.End.X + 12;
        var y = arrow.End.Y + 12;

        if (x + MemoWidth > virtualBounds.Right)
        {
            x = arrow.End.X - MemoWidth - 12;
        }

        if (y + MemoHeight > virtualBounds.Bottom)
        {
            y = arrow.End.Y - MemoHeight - 12;
        }

        x = Math.Clamp(x, virtualBounds.Left, Math.Max(virtualBounds.Left, virtualBounds.Right - MemoWidth));
        y = Math.Clamp(y, virtualBounds.Top, Math.Max(virtualBounds.Top, virtualBounds.Bottom - MemoHeight));
        return new Rectangle(x, y, MemoWidth, MemoHeight);
    }
}
