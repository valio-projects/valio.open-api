namespace ValIO.Core.IR;

/// <summary>
/// Граф зависимостей типов
/// Используется для определения порядка генерации моделей и импортов
/// </summary>
public sealed record IrTypeDependencyGraph
{
    /// <summary>
    /// Зависимости типов (ключ - имя типа, значение - список зависимых типов)
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Dependencies { get; init; }
    
    /// <summary>
    /// Топологический порядок типов (для генерации в правильном порядке)
    /// </summary>
    public required IReadOnlyList<string> TopologicalOrder { get; init; }
}
