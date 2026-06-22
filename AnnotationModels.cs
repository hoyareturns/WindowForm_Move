namespace WindowForm_Move;

public abstract record AnnotationItem;

public sealed record ScreenMarker(int Number, Point Location, Color Color) : AnnotationItem;

public sealed record ScreenDot(Point Location, Color Color) : AnnotationItem;

public sealed record ScreenStroke(List<Point> Points, Color Color, float Width) : AnnotationItem;

public sealed record AnnotationArrow(Point Start, Point End, Color Color, float Width) : AnnotationItem
{
    public string Text { get; set; } = string.Empty;
}

public enum AnnotationTool
{
    None,
    Dot,
    Marker,
    Arrow,
    Pencil,
    Eraser
}
