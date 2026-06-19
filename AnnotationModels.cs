namespace WindowForm_Move;

public abstract record AnnotationItem;

public sealed record ScreenMarker(int Number, Point Location) : AnnotationItem;

public sealed record AnnotationArrow(Point Start, Point End, Color Color, float Width) : AnnotationItem
{
    public string Text { get; set; } = string.Empty;
}

public enum AnnotationTool
{
    None,
    Marker,
    Arrow,
    Eraser
}
