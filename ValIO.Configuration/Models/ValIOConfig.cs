using ValIO.Core.Models;

namespace ValIO.Configuration.Models;

/// <summary>
/// Корневая модель конфигурации (соответствует JSON/YAML)
/// </summary>
public class ValIOConfig
{
    public string Language { get; set; } = string.Empty;
    public ConfigOptions? Options { get; set; }
    public ConfigFileOrganization? FileOrganization { get; set; }
    public ConfigNamingConventions? NamingConvention { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public bool GenerateEnums { get; set; } = true;
    public bool GenerateModels { get; set; } = true;
    public bool GenerateClients { get; set; } = true;
}

/// <summary>
/// Опции конфигурации
/// </summary>
public class ConfigOptions
{
    public string? ValidationLibrary { get; set; }
    public string? HttpClientLibrary { get; set; }
    public string? QueryWrapperLibrary { get; set; }
    
    /// <summary>
    /// C# специфичные опции
    /// </summary>
    public ConfigCSharpOptions? CSharp { get; set; }
}

/// <summary>
/// C# специфичные опции конфигурации
/// </summary>
public class ConfigCSharpOptions
{
    /// <summary>
    /// Тип аксессора свойства: "Set" или "Init"
    /// </summary>
    public string? PropertyAccessor { get; set; }
    
    /// <summary>
    /// Использовать required для обязательных свойств
    /// </summary>
    public bool? UseRequired { get; set; }
    
    /// <summary>
    /// Делать классы sealed
    /// </summary>
    public bool? UseSealed { get; set; }
    
    /// <summary>
    /// Тип модели: "Class", "Record", "Struct", "RecordStruct"
    /// </summary>
    public string? ModelType { get; set; }
    
    /// <summary>
    /// Генерировать методы сравнения для record
    /// </summary>
    public bool? GenerateEqualityMembers { get; set; }
    
    /// <summary>
    /// Использовать nullable reference types ("?") для nullable свойств
    /// </summary>
    public bool? UseNullable { get; set; }
    
    /// <summary>
    /// Генерировать статические классы с константами валидации
    /// </summary>
    public bool? GenerateValidationConstants { get; set; }

    /// <summary>
    /// Генерировать атрибуты валидации для свойств ([Required], [MinLength], [MaxLength], [Range], [RegularExpression])
    /// </summary>
    public bool? GenerateValidationAttributes { get; set; }

    /// <summary>
    /// Стиль передачи query параметров: "IndividualParameters" или "ModelClass"
    /// </summary>
    public string? QueryParametersStyle { get; set; }
}

/// <summary>
/// Организация файлов в конфигурации
/// </summary>
public class ConfigFileOrganization
{
    public string GroupingStrategy { get; set; } = "SingleFile";
    public bool SeparateModels { get; set; } = true;
    public bool SeparateClients { get; set; } = true;
    public string? ModelsPath { get; set; }
    public string? ClientsPath { get; set; }
}

/// <summary>
/// Конвенции именования в конфигурации
/// </summary>
public class ConfigNamingConventions
{
    public string Models { get; set; } = "PascalCase";
    public string Properties { get; set; } = "CamelCase";
    public string Methods { get; set; } = "CamelCase";
    public string Enums { get; set; } = "PascalCase";
}

