namespace ValIO.Core.IR.Types;

/// <summary>
/// Intersection (allOf)
/// </summary>
public sealed record IrIntersectionType : IrType
{
    /// <summary>
    /// Типы в intersection
    /// </summary>
    public required IReadOnlyList<IrType> Types { get; init; }
}
