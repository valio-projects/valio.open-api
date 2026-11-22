namespace ValIO.Generators.Cpp.Models;

/// <summary>
/// Спецификация query параметра для Native API функции.
/// </summary>
public sealed class QueryParamSpec : IEquatable<QueryParamSpec>
{
    /// <summary>Gets the wire name used in the query string.</summary>
    /// <value>The original OpenAPI parameter name.</value>
    public required string Name { get; init; }

    /// <summary>Gets the C++ member that supplies the query value.</summary>
    /// <value>The generated property identifier.</value>
    public required string PropertyName { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(QueryParamSpec? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Name, other.Name)
        && EqualityComparer<string>.Default.Equals(PropertyName, other.PropertyName);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is QueryParamSpec other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, PropertyName);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(QueryParamSpec? left, QueryParamSpec? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(QueryParamSpec? left, QueryParamSpec? right) => !Equals(left, right);
}
