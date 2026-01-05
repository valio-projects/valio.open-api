namespace ValIO.Core.IR;

/// <summary>
/// Корневой узел IR представления OpenAPI схемы
/// </summary>
public sealed record IrDocument
{
    /// <summary>
    /// Метаданные документа
    /// </summary>
    public required IrDocumentMetadata Metadata { get; init; }
    
    /// <summary>
    /// Модели (схемы)
    /// </summary>
    public required IReadOnlyList<IrModel> Models { get; init; }
    
    /// <summary>
    /// Enum'ы
    /// </summary>
    public required IReadOnlyList<IrEnum> Enums { get; init; }
    
    /// <summary>
    /// HTTP клиенты
    /// </summary>
    public required IReadOnlyList<IrClient> Clients { get; init; }
    
    /// <summary>
    /// Граф зависимостей типов
    /// </summary>
    public required IrTypeDependencyGraph TypeDependencies { get; init; }
}
