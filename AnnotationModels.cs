namespace WindowForm_Move;

public abstract record AnnotationItem;

public sealed record ScreenMarker(int Number, Point Location, Color Color, int Size) : AnnotationItem;

public sealed record ScreenDot(Point Location, Color Color, int Size) : AnnotationItem;

public sealed record ScreenStroke(List<Point> Points, Color Color, float Width) : AnnotationItem;

public sealed record ScreenShape(
    AnnotationShapeKind Kind,
    Point Start,
    Point End,
    Color Color,
    float Width,
    int MarkerSize) : AnnotationItem;

public sealed record ScreenText(Point Location, Color Color) : AnnotationItem
{
    public string Text { get; set; } = string.Empty;
    public string FontName { get; set; } = "Segoe UI Semibold";
    public float FontSize { get; set; } = 10F;
}

public sealed record AnnotationArrow(Point Start, Point End, Color Color, float Width) : AnnotationItem
{
    public string Text { get; set; } = string.Empty;
    public string FontName { get; set; } = "Segoe UI Semibold";
    public float FontSize { get; set; } = 10F;
}

public enum AnnotationTool
{
    None,
    Dot,
    Marker,
    Arrow,
    Pencil,
    Text,
    FilledSquare,
    DoubleArrow,
    Rectangle,
    Ellipse,
    Line,
    HorizontalLine,
    VerticalLine,
    Moving,
    Eraser
}

public enum AnnotationShapeKind
{
    FilledSquare,
    DoubleArrow,
    Rectangle,
    Ellipse,
    Line,
    HorizontalLine,
    VerticalLine
}
