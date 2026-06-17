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
public sealed record NameAffixOptions
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
}
