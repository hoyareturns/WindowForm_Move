namespace WindowForm_Move;

public sealed record AnnotationTarget(
    string Id,
    string DisplayName,
    Rectangle Bounds,
    bool RequiresSelection = false)
{
    public override string ToString() => DisplayName;
}
