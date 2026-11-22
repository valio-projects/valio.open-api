using System.Collections.Immutable;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;

/// <summary>
/// Спецификаторы макроса UCLASS(...).
/// Определяет поведение класса в Unreal Engine Reflection System.
/// </summary>
public sealed class UClassSpecifiers : IEquatable<UClassSpecifiers>
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

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(UClassSpecifiers? other) => other is not null
        && EqualityComparer<string?>.Default.Equals(Category, other.Category)
        && EqualityComparer<IReadOnlyDictionary<string, string>>.Default.Equals(Meta, other.Meta);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UClassSpecifiers other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Category, Meta);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(UClassSpecifiers? left, UClassSpecifiers? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(UClassSpecifiers? left, UClassSpecifiers? right) => !Equals(left, right);
}
