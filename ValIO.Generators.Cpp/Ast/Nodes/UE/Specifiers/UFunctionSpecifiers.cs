using System.Collections.Immutable;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;

/// <summary>
/// Спецификаторы макроса UFUNCTION(...).
/// Определяет поведение функции/метода в Unreal Engine Reflection System и Blueprints.
/// </summary>
public sealed record UFunctionSpecifiers
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
}
