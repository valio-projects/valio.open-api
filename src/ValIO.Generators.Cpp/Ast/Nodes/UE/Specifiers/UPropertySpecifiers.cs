using System.Collections.Immutable;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;

/// <summary>
/// Спецификаторы макроса UPROPERTY(...).
/// Определяет поведение свойства/поля в Unreal Engine Reflection System и Blueprints.
/// </summary>
public sealed class UPropertySpecifiers : IEquatable<UPropertySpecifiers>
{
    /// <summary>
    /// EditAnywhere — свойство можно редактировать в Details Panel
    /// </summary>
    public bool EditAnywhere { get; init; }

    /// <summary>
    /// BlueprintReadWrite — свойство доступно для чтения и записи в Blueprints
    /// </summary>
    public bool BlueprintReadWrite { get; init; }

    /// <summary>
    /// BlueprintReadOnly — свойство доступно только для чтения в Blueprints
    /// </summary>
    public bool BlueprintReadOnly { get; init; }

    /// <summary>
    /// BlueprintAssignable — делегат может быть привязан в Blueprint
    /// </summary>
    public bool BlueprintAssignable { get; init; }

    /// <summary>
    /// Category для группировки в Blueprint Editor и Details Panel
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// DisplayName — отображаемое имя в Blueprint Editor
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Meta-спецификаторы: meta = (Key = "Value", ...).
    /// Пример: meta = (DisplayName = "On Completed")
    /// </summary>
    public IReadOnlyDictionary<string, string> Meta { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// Предустановка: свойство модели (BlueprintReadWrite, Category)
    /// </summary>
    public static UPropertySpecifiers ModelField(string category) => new()
    {
        BlueprintReadWrite = true,
        Category = category
    };

    /// <summary>
    /// Предустановка: параметр для Blueprint (EditAnywhere + BlueprintReadWrite)
    /// </summary>
    public static UPropertySpecifiers ParamField(string category) => new()
    {
        EditAnywhere = true,
        BlueprintReadWrite = true,
        Category = category
    };

    /// <summary>
    /// Предустановка: результат (BlueprintReadOnly)
    /// </summary>
    public static UPropertySpecifiers ResultField(string? category = null) => new()
    {
        BlueprintReadOnly = true,
        Category = category
    };

    /// <summary>
    /// Предустановка: Blueprint-assignable делегат
    /// </summary>
    public static UPropertySpecifiers AssignableDelegate(string? displayName = null) => new()
    {
        BlueprintAssignable = true,
        Meta = displayName != null
            ? new Dictionary<string, string> { ["DisplayName"] = displayName }.ToImmutableDictionary()
            : ImmutableDictionary<string, string>.Empty
    };

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(UPropertySpecifiers? other) => other is not null
        && EqualityComparer<bool>.Default.Equals(EditAnywhere, other.EditAnywhere)
        && EqualityComparer<bool>.Default.Equals(BlueprintReadWrite, other.BlueprintReadWrite)
        && EqualityComparer<bool>.Default.Equals(BlueprintReadOnly, other.BlueprintReadOnly)
        && EqualityComparer<bool>.Default.Equals(BlueprintAssignable, other.BlueprintAssignable)
        && EqualityComparer<string?>.Default.Equals(Category, other.Category)
        && EqualityComparer<string?>.Default.Equals(DisplayName, other.DisplayName)
        && EqualityComparer<IReadOnlyDictionary<string, string>>.Default.Equals(Meta, other.Meta);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UPropertySpecifiers other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(EditAnywhere, BlueprintReadWrite, BlueprintReadOnly, BlueprintAssignable, Category, DisplayName, Meta);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(UPropertySpecifiers? left, UPropertySpecifiers? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(UPropertySpecifiers? left, UPropertySpecifiers? right) => !Equals(left, right);
}
