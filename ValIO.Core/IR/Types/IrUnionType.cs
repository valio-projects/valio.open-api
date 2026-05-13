namespace ValIO.Core.IR.Types;

/// <summary>
/// Union (oneOf, anyOf)
/// </summary>
public sealed record IrUnionType : IrType
{
    /// <summary>
    /// Типы в union
    /// </summary>
    public required IReadOnlyList<IrType> Types { get; init; }
}
