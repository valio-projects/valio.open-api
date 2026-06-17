namespace ValIO.Core.Models;

/// <summary>
/// Настройки конвенций именования
/// </summary>
public class NamingConventions
{
    public NamingConvention Models { get; set; } = NamingConvention.PascalCase;
    public NamingConvention Properties { get; set; } = NamingConvention.CamelCase;
    public NamingConvention Methods { get; set; } = NamingConvention.CamelCase;
    public NamingConvention Enums { get; set; } = NamingConvention.PascalCase;

    // Опциональные prefix/suffix к ИДЕНТИФИКАТОРАМ генерируемых сущностей. Не влияют на
    // имена файлов и пути импорта. Пустая строка = affix не применяется. Применяются
    // кросс-языково на уровне IR (см. ValIO.Core.IR.Builders.IrNameAffixer).

    /// <summary>Prefix к именам моделей (DTO), напр. "Api" → ApiCreateJournalSchema.</summary>
    public string ModelPrefix { get; set; } = string.Empty;

    /// <summary>Suffix к именам моделей (DTO), напр. "Request" → CreateJournalSchemaRequest.</summary>
    public string ModelSuffix { get; set; } = string.Empty;

    /// <summary>Prefix к именам методов/операций (client-функций).</summary>
    public string MethodPrefix { get; set; } = string.Empty;

    /// <summary>Suffix к именам методов/операций, напр. "Method" → CreateJournalSchemaMethod.</summary>
    public string MethodSuffix { get; set; } = string.Empty;

    /// <summary>Prefix к именам enum'ов.</summary>
    public string EnumPrefix { get; set; } = string.Empty;

    /// <summary>Suffix к именам enum'ов.</summary>
    public string EnumSuffix { get; set; } = string.Empty;

    // Affix для per-operation result/response-union типа (TypeScript) задаётся в опциях
    // генератора (options.resultTypePrefix / options.resultTypeSuffix), а не здесь, т.к. это
    // TS-специфичный синтезированный тип (как и существующий resultTypeSuffix).
}

