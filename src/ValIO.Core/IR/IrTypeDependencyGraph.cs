namespace ValIO.Core.IR;

/// <summary>
/// Граф зависимостей типов
/// Используется для определения порядка генерации моделей и импортов
/// </summary>
public sealed class IrTypeDependencyGraph : IEquatable<IrTypeDependencyGraph>
{
    /// <summary>
    /// Зависимости типов (ключ - имя типа, значение - список зависимых типов)
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Dependencies { get; init; }
    
    /// <summary>
    /// Топологический порядок типов (для генерации в правильном порядке)
    /// </summary>
    public required IReadOnlyList<string> TopologicalOrder { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrTypeDependencyGraph? other) => other is not null
        && EqualityComparer<IReadOnlyDictionary<string, IReadOnlyList<string>>>.Default.Equals(Dependencies, other.Dependencies)
        && EqualityComparer<IReadOnlyList<string>>.Default.Equals(TopologicalOrder, other.TopologicalOrder);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrTypeDependencyGraph other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Dependencies, TopologicalOrder);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrTypeDependencyGraph? left, IrTypeDependencyGraph? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrTypeDependencyGraph? left, IrTypeDependencyGraph? right) => !Equals(left, right);
}
