namespace ValIO.Core.IR.Builders;

/// <summary>
/// Настройки добавления опциональных prefix/suffix к именам генерируемых сущностей
/// (моделей, методов/операций, enum'ов). Применяются один раз на уровне IR
/// (см. <see cref="IrNameAffixer"/>), благодаря чему изменение имени автоматически
/// распространяется на все определения, ссылки, импорты и граф зависимостей во всех языках.
///
/// Affix НЕ влияет на имена файлов и пути импорта — для них используются исходные
/// (Original*) имена IR-сущностей.
/// </summary>
public sealed class NameAffixOptions : IEquatable<NameAffixOptions>
{
    /// <summary>Prefix для имён моделей (DTO).</summary>
    public string ModelPrefix { get; init; } = string.Empty;

    /// <summary>Suffix для имён моделей (DTO).</summary>
    public string ModelSuffix { get; init; } = string.Empty;

    /// <summary>Prefix для имён методов/операций (client-функций).</summary>
    public string MethodPrefix { get; init; } = string.Empty;

    /// <summary>Suffix для имён методов/операций (client-функций).</summary>
    public string MethodSuffix { get; init; } = string.Empty;

    /// <summary>Prefix для имён enum'ов.</summary>
    public string EnumPrefix { get; init; } = string.Empty;

    /// <summary>Suffix для имён enum'ов.</summary>
    public string EnumSuffix { get; init; } = string.Empty;

    /// <summary>
    /// True, если ни один affix не задан — преобразование можно пропустить целиком
    /// (поведение генерации идентично прежнему).
    /// </summary>
    public bool IsNoOp =>
        ModelPrefix.Length == 0 && ModelSuffix.Length == 0 &&
        MethodPrefix.Length == 0 && MethodSuffix.Length == 0 &&
        EnumPrefix.Length == 0 && EnumSuffix.Length == 0;

    /// <summary>Пустые настройки (affix не применяется).</summary>
    public static readonly NameAffixOptions None = new();

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(NameAffixOptions? other) => other is not null
        && EqualityComparer<string>.Default.Equals(ModelPrefix, other.ModelPrefix)
        && EqualityComparer<string>.Default.Equals(ModelSuffix, other.ModelSuffix)
        && EqualityComparer<string>.Default.Equals(MethodPrefix, other.MethodPrefix)
        && EqualityComparer<string>.Default.Equals(MethodSuffix, other.MethodSuffix)
        && EqualityComparer<string>.Default.Equals(EnumPrefix, other.EnumPrefix)
        && EqualityComparer<string>.Default.Equals(EnumSuffix, other.EnumSuffix);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is NameAffixOptions other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(ModelPrefix, ModelSuffix, MethodPrefix, MethodSuffix, EnumPrefix, EnumSuffix);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(NameAffixOptions? left, NameAffixOptions? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(NameAffixOptions? left, NameAffixOptions? right) => !Equals(left, right);
}
