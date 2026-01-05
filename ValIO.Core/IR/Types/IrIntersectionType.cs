using ValIO.Core.IR.Enums;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Intersection (allOf)
/// </summary>
public sealed record IrIntersectionType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Intersection;
    
    /// <summary>
    /// Типы в intersection
    /// </summary>
    public required IReadOnlyList<IrType> Types { get; init; }
}
