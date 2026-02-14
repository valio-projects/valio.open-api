using System.Collections.Immutable;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;

/// <summary>
/// Спецификаторы макроса USTRUCT(...).
/// Определяет поведение структуры в Unreal Engine Reflection System.
/// </summary>
public sealed record UStructSpecifiers
{
    /// <summary>
    /// Включить BlueprintType — позволяет использовать структуру в Blueprints.
    /// USTRUCT(BlueprintType)
    /// </summary>
    public bool BlueprintType { get; init; } = true;

    /// <summary>
    /// Meta-спецификаторы: meta = (Key = "Value", ...).
    /// Пример: meta = (ShortToolTip = "Result of Get Queue Tasks List")
    /// </summary>
    public IReadOnlyDictionary<string, string> Meta { get; init; } =
        ImmutableDictionary<string, string>.Empty;
}
