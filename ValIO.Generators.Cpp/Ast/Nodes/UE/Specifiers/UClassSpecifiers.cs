using System.Collections.Immutable;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;

/// <summary>
/// Спецификаторы макроса UCLASS(...).
/// Определяет поведение класса в Unreal Engine Reflection System.
/// </summary>
public sealed record UClassSpecifiers
{
    /// <summary>
    /// Meta-спецификаторы: meta = (Key = "Value", ...).
    /// Пример: meta = (ExposedAsyncProxy = "AsyncTask")
    /// </summary>
    public IReadOnlyDictionary<string, string> Meta { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// Предустановка: async action class с ExposedAsyncProxy
    /// </summary>
    public static UClassSpecifiers AsyncAction() => new()
    {
        Meta = new Dictionary<string, string>
        {
            ["ExposedAsyncProxy"] = "AsyncTask"
        }.ToImmutableDictionary()
    };
}
