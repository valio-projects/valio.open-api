using System.Collections.Immutable;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;

/// <summary>
/// Спецификаторы макроса UCLASS(...).
/// Определяет поведение класса в Unreal Engine Reflection System.
/// </summary>
public sealed record UClassSpecifiers
{
    /// <summary>
    /// Category для группировки в Blueprint Editor.
    /// UCLASS(Category = "ValIO|Panoramas|GetQueueTasksList")
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Meta-спецификаторы: meta = (Key = "Value", ...).
    /// Пример: meta = (ExposedAsyncProxy = "AsyncTask")
    /// </summary>
    public IReadOnlyDictionary<string, string> Meta { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// Предустановка: async action class с ExposedAsyncProxy
    /// </summary>
    public static UClassSpecifiers AsyncAction(string? category = null) => new()
    {
        Category = category,
        Meta = new Dictionary<string, string>
        {
            ["ExposedAsyncProxy"] = "AsyncTask"
        }.ToImmutableDictionary()
    };

    /// <summary>
    /// Предустановка: paths library class
    /// </summary>
    public static UClassSpecifiers PathsLibrary(string? category = null) => new()
    {
        Category = category
    };
}
