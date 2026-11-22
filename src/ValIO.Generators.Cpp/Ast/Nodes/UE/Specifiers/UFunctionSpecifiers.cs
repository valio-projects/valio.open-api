using System.Collections.Immutable;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;

/// <summary>
/// Спецификаторы макроса UFUNCTION(...).
/// Определяет поведение функции/метода в Unreal Engine Reflection System и Blueprints.
/// </summary>
public sealed class UFunctionSpecifiers : IEquatable<UFunctionSpecifiers>
{
    /// <summary>
    /// BlueprintCallable — функцию можно вызвать из Blueprint
    /// </summary>
    public bool BlueprintCallable { get; init; }

    /// <summary>
    /// BlueprintPure — чистая функция без side-effects (не требует Exec pin)
    /// </summary>
    public bool BlueprintPure { get; init; }

    /// <summary>
    /// Category для группировки в Blueprint Editor
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Meta-спецификаторы: meta = (Key = "Value", ...).
    /// Пример: meta = (BlueprintInternalUseOnly = "true", WorldContext = "WorldContextObject")
    /// </summary>
    public IReadOnlyDictionary<string, string> Meta { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// Предустановка: BlueprintPure path builder
    /// </summary>
    public static UFunctionSpecifiers PathBuilder(string category, string? displayName = null)
    {
        var meta = new Dictionary<string, string>();
        if (displayName != null)
            meta["DisplayName"] = displayName;

        return new UFunctionSpecifiers
        {
            BlueprintPure = true,
            Category = category,
            Meta = meta.ToImmutableDictionary()
        };
    }

    /// <summary>
    /// Предустановка: BlueprintCallable BuildRequestBuilder
    /// </summary>
    public static UFunctionSpecifiers BuildRequestBuilder(string category, string? displayName = null) => new()
    {
        BlueprintCallable = true,
        Category = category,
        Meta = displayName != null
            ? new Dictionary<string, string> { ["DisplayName"] = displayName }.ToImmutableDictionary()
            : ImmutableDictionary<string, string>.Empty
    };

    /// <summary>
    /// Предустановка: BlueprintCallable async factory method
    /// </summary>
    public static UFunctionSpecifiers AsyncFactory(string category, string? displayName = null) => new()
    {
        BlueprintCallable = true,
        Category = category,
        Meta = new Dictionary<string, string>
        {
            ["BlueprintInternalUseOnly"] = "true",
            ["WorldContext"] = "WorldContextObject",
            ["DisplayName"] = displayName ?? ""
        }.Where(kv => !string.IsNullOrEmpty(kv.Value))
         .ToImmutableDictionary(kv => kv.Key, kv => kv.Value)
    };

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(UFunctionSpecifiers? other) => other is not null
        && EqualityComparer<bool>.Default.Equals(BlueprintCallable, other.BlueprintCallable)
        && EqualityComparer<bool>.Default.Equals(BlueprintPure, other.BlueprintPure)
        && EqualityComparer<string?>.Default.Equals(Category, other.Category)
        && EqualityComparer<IReadOnlyDictionary<string, string>>.Default.Equals(Meta, other.Meta);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UFunctionSpecifiers other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(BlueprintCallable, BlueprintPure, Category, Meta);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(UFunctionSpecifiers? left, UFunctionSpecifiers? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(UFunctionSpecifiers? left, UFunctionSpecifiers? right) => !Equals(left, right);
}
