namespace ValIO.Core.IR;

/// <summary>
/// Корневой узел IR представления OpenAPI схемы
/// </summary>
public sealed class IrDocument : IEquatable<IrDocument>
{
    /// <summary>Creates a language-independent value for initialization from source data.</summary>
    public IrDocument()
    {
    }

    /// <summary>Copies all stored members before applying initializer overrides.</summary>
    /// <param name="source">The value to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrDocument(IrDocument source)
    {
        Metadata = source.Metadata;
        Models = source.Models;
        Enums = source.Enums;
        Clients = source.Clients;
        TypeDependencies = source.TypeDependencies;
    }

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

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrDocument? other) => other is not null
        && EqualityComparer<IrDocumentMetadata>.Default.Equals(Metadata, other.Metadata)
        && EqualityComparer<IReadOnlyList<IrModel>>.Default.Equals(Models, other.Models)
        && EqualityComparer<IReadOnlyList<IrEnum>>.Default.Equals(Enums, other.Enums)
        && EqualityComparer<IReadOnlyList<IrClient>>.Default.Equals(Clients, other.Clients)
        && EqualityComparer<IrTypeDependencyGraph>.Default.Equals(TypeDependencies, other.TypeDependencies);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrDocument other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Metadata, Models, Enums, Clients, TypeDependencies);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrDocument? left, IrDocument? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrDocument? left, IrDocument? right) => !Equals(left, right);
}
