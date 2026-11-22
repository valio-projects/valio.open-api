namespace ValIO.Core.IR;

/// <summary>
/// IR представление HTTP клиента
/// </summary>
public sealed class IrClient : IEquatable<IrClient>
{
    /// <summary>Creates a language-independent value for initialization from source data.</summary>
    public IrClient()
    {
    }

    /// <summary>Copies all stored members before applying initializer overrides.</summary>
    /// <param name="source">The value to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrClient(IrClient source)
    {
        Name = source.Name;
        Operations = source.Operations;
    }

    /// <summary>
    /// Имя клиента (sanitized)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Операции клиента
    /// </summary>
    public required IReadOnlyList<IrOperation> Operations { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrClient? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Name, other.Name)
        && EqualityComparer<IReadOnlyList<IrOperation>>.Default.Equals(Operations, other.Operations);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrClient other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, Operations);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrClient? left, IrClient? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrClient? left, IrClient? right) => !Equals(left, right);
}
