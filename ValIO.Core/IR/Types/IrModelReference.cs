namespace ValIO.Core.IR.Types;

/// <summary>
/// Ссылка на модель
/// </summary>
public sealed record IrModelReference : IrType
{
    /// <summary>
    /// Имя модели
    /// </summary>
    public required string ModelName { get; init; }
}
