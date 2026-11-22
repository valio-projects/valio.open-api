namespace ValIO.Core.IR;

/// <summary>
/// Метаданные IR документа
/// </summary>
public sealed class IrDocumentMetadata : IEquatable<IrDocumentMetadata>
{
    /// <summary>
    /// Название API
    /// </summary>
    public string? Title { get; init; }
    
    /// <summary>
    /// Версия API
    /// </summary>
    public string? Version { get; init; }
    
    /// <summary>
    /// Описание API
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Базовый URL сервера
    /// </summary>
    public string? ServerUrl { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrDocumentMetadata? other) => other is not null
        && EqualityComparer<string?>.Default.Equals(Title, other.Title)
        && EqualityComparer<string?>.Default.Equals(Version, other.Version)
        && EqualityComparer<string?>.Default.Equals(Description, other.Description)
        && EqualityComparer<string?>.Default.Equals(ServerUrl, other.ServerUrl);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrDocumentMetadata other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Title, Version, Description, ServerUrl);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrDocumentMetadata? left, IrDocumentMetadata? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrDocumentMetadata? left, IrDocumentMetadata? right) => !Equals(left, right);
}
