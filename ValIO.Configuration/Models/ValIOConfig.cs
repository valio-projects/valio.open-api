using ValIO.Core.Models;

namespace ValIO.Configuration.Models;

/// <summary>
/// Опции конфигурации
/// </summary>
public class ConfigOptions
{
    public string? ValidationLibrary { get; set; }
    public string? QueryWrapperLibrary { get; set; }
    
    /// <summary>
    /// C# специфичные опции
    /// </summary>
    public ConfigCSharpOptions? CSharp { get; set; }
    
    /// <summary>
    /// TypeScript специфичные опции
    /// </summary>
    public ConfigTypeScriptOptions? TypeScript { get; set; }
    
    /// <summary>
    /// C++/Unreal Engine специфичные опции
    /// </summary>
    public ConfigCppOptions? Cpp { get; set; }
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
    
    /// <summary>
    /// Генерировать интерфейсы для HTTP клиентов
    /// </summary>
    public bool? GenerateInterfaces { get; set; }
    
    /// <summary>
    /// Стратегия размещения интерфейсов: "SameFile" или "SeparateFile"
    /// </summary>
    public string? InterfaceFileStrategy { get; set; }
    
    /// <summary>
    /// Модификатор доступа для классов HTTP клиентов: "Public" или "Internal"
    /// </summary>
    public string? ClientAccessModifier { get; set; }
    
    /// <summary>
    /// Генерировать traces (spans, activities) для OpenTelemetry
    /// </summary>
    public bool? GenerateTraces { get; set; }
    
    /// <summary>
    /// Генерировать metrics для OpenTelemetry
    /// </summary>
    public bool? GenerateMetrics { get; set; }
}

/// <summary>
/// TypeScript специфичные опции конфигурации
/// </summary>
public class ConfigTypeScriptOptions
{
    /// <summary>
    /// Формат моделей: "Interfaces" или "ValidationSchemas"
    /// </summary>
    public string? ModelFormat { get; set; }
    
    /// <summary>
    /// Библиотека валидации: "Zod", "Yup", "Valli"
    /// </summary>
    public string? ValidationLibrary { get; set; }
    
    /// <summary>
    /// Транспорты: "fetch", "axios", "both" или через запятую "axios,fetch".
    /// </summary>
    public string? HttpTransports { get; set; }
    
    /// <summary>
    /// Использовать TanStack Query
    /// </summary>
    public bool? UseTanStackQuery { get; set; }

    /// <summary>
    /// Целевой фреймворк для TanStack обёрток: "react" (по умолчанию), "angular", "solid", "svelte".
    /// </summary>
    public string? TanStackFramework { get; set; }
    
    /// <summary>
    /// Сериализовать/валидировать payload через схемы
    /// </summary>
    public bool? SerializeWithSchemas { get; set; }
    
    /// <summary>
    /// Десериализовать/валидировать response через схемы
    /// </summary>
    public bool? DeserializeWithSchemas { get; set; }
    
    /// <summary>
    /// Формат enum'ов: "Enum" или "UnionType"
    /// </summary>
    public string? EnumFormat { get; set; }
    
    /// <summary>
    /// Использовать strict null checks
    /// </summary>
    public bool? UseStrictNullChecks { get; set; }
    
    /// <summary>
    /// Использовать нативный bigint для int64 (format: int64). По умолчанию false — используется number.
    /// </summary>
    public bool? UseBigInt { get; set; }
    
    /// <summary>
    /// Настройки для Axios HTTP клиента
    /// </summary>
    public ConfigAxiosOptions? Axios { get; set; }

    /// <summary>
    /// Настройки для fetch HTTP клиента (кастомный fetcher)
    /// </summary>
    public ConfigFetchOptions? Fetch { get; set; }
    
    /// <summary>
    /// Стиль передачи query параметров: "IndividualParameters" или "ModelClass"
    /// </summary>
    public string? QueryParametersStyle { get; set; }
}

/// <summary>
/// Настройки для fetch HTTP клиента
/// </summary>
public class ConfigFetchOptions
{
    /// <summary>
    /// Путь до модуля с кастомной функцией fetch
    /// </summary>
    public string? CustomFetcherPath { get; set; }

    /// <summary>
    /// Имя экспорта fetcher-функции
    /// </summary>
    public string? CustomFetcherName { get; set; }
}

/// <summary>
/// Настройки для Axios HTTP клиента
/// </summary>
public class ConfigAxiosOptions
{
    /// <summary>
    /// Путь до файла с custom axios instance (например, '../mutator/custom-instance')
    /// </summary>
    public string? CustomInstancePath { get; set; }
    
    /// <summary>
    /// Название константы с custom axios instance (например, 'customInstance')
    /// </summary>
    public string? CustomInstanceName { get; set; }
}

/// <summary>
/// C++/Unreal Engine специфичные опции конфигурации
/// </summary>
public class ConfigCppOptions
{
    /// <summary>
    /// Макрос API модуля UE (например "WIREFRAME_API", "MYPROJECT_API")
    /// </summary>
    public string? ModuleApiMacro { get; set; }
    
    /// <summary>
    /// Категория по умолчанию для UPROPERTY/UFUNCTION в Blueprint Editor
    /// </summary>
    public string? BlueprintCategory { get; set; }
    
    /// <summary>
    /// Текст copyright в заголовке файлов
    /// </summary>
    public string? Copyright { get; set; }
    
    /// <summary>
    /// Имя базового класса для async actions
    /// </summary>
    public string? BaseAsyncActionClassName { get; set; }
    
    /// <summary>
    /// Include path для базового async action класса
    /// </summary>
    public string? BaseAsyncActionIncludePath { get; set; }

    /// <summary>
    /// Добавлять UE-инклюды (CoreMinimal, Async/Future, ValIOResponse) в headers.
    /// </summary>
    public bool AddUnrealEngineIncludesInHeaders { get; set; } = true;
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

