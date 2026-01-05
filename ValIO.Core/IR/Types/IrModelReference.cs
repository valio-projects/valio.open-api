using ValIO.Core.IR.Enums;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Ссылка на модель
/// </summary>
public sealed record IrModelReference : IrType
{
    public override IrTypeKind Kind => IrTypeKind.ModelReference;
    
    /// <summary>
    /// Имя модели
    /// </summary>
    public required string ModelName { get; init; }
}
