namespace ValIO.Generators.Cpp.Models;

/// <summary>
/// Спецификация параметра пути для Path builder метода.
/// </summary>
public sealed class PathParamSpec : IEquatable<PathParamSpec>
{
    /// <summary>Имя в шаблоне URL (для замены {tenantId})</summary>
    public required string OriginalName { get; init; }

    /// <summary>Имя параметра в C++ (TenantId)</summary>
    public required string PropertyName { get; init; }

    /// <summary>Спецификатор printf (%s, %lld)</summary>
    public required string PrintfSpecifier { get; init; }

    /// <summary>Выражение для передачи в Printf (прямое или через промежуточную переменную)</summary>
    public required string PrintfArgExpression { get; init; }

    /// <summary>Требуется ли промежуточная переменная (FGuid → ToString DigitsWithHyphens)</summary>
    public bool IsGuid { get; init; }

    /// <summary>Имя промежуточной переменной для FGuid (TenantIdStr)</summary>
    public string? IntermediateVarName { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(PathParamSpec? other) => other is not null
        && EqualityComparer<string>.Default.Equals(OriginalName, other.OriginalName)
        && EqualityComparer<string>.Default.Equals(PropertyName, other.PropertyName)
        && EqualityComparer<string>.Default.Equals(PrintfSpecifier, other.PrintfSpecifier)
        && EqualityComparer<string>.Default.Equals(PrintfArgExpression, other.PrintfArgExpression)
        && EqualityComparer<bool>.Default.Equals(IsGuid, other.IsGuid)
        && EqualityComparer<string?>.Default.Equals(IntermediateVarName, other.IntermediateVarName);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PathParamSpec other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(OriginalName, PropertyName, PrintfSpecifier, PrintfArgExpression, IsGuid, IntermediateVarName);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(PathParamSpec? left, PathParamSpec? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(PathParamSpec? left, PathParamSpec? right) => !Equals(left, right);
}
