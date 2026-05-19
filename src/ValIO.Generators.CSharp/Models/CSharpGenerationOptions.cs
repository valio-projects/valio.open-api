using ValIO.Core.Ast.Enums;

namespace ValIO.Generators.CSharp.Models;

/// <summary>
/// Опции генерации C# кода
/// </summary>
public class CSharpGenerationOptions
{
    /// <summary>
    /// Тип свойства: set или init
    /// </summary>
    public PropertyAccessorType PropertyAccessor { get; set; } = PropertyAccessorType.Set;

    /// <summary>
    /// Использовать required для обязательных свойств
    /// </summary>
    public bool UseRequired { get; set; } = false;

    /// <summary>
    /// Делать классы sealed
    /// </summary>
    public bool UseSealed { get; set; } = false;

    /// <summary>
    /// Тип модели: class, record или struct
    /// </summary>
    public ModelType ModelType { get; set; } = ModelType.Class;

    /// <summary>
    /// Генерировать методы сравнения (Equals, GetHashCode) для record
    /// </summary>
    public bool GenerateEqualityMembers { get; set; } = true;

    /// <summary>
    /// Использовать nullable reference types ("?") для nullable свойств
    /// </summary>
    public bool UseNullable { get; set; } = true;

    /// <summary>
    /// Генерировать статические классы с константами (Min, Max, MinLength, MaxLength, DefaultValue)
    /// </summary>
    public bool GenerateValidationConstants { get; set; } = false;

    /// <summary>
    /// Генерировать атрибуты валидации для свойств ([Required], [MinLength], [MaxLength], [Range], [RegularExpression])
    /// </summary>
    public bool GenerateValidationAttributes { get; set; } = false;

    /// <summary>
    /// Стиль передачи query параметров
    /// </summary>
    public QueryParametersStyle QueryParametersStyle { get; set; } = QueryParametersStyle.IndividualParameters;
    
    /// <summary>
    /// Генерировать интерфейсы для HTTP клиентов
    /// </summary>
    public bool GenerateInterfaces { get; set; } = false;
    
    /// <summary>
    /// Стратегия размещения интерфейсов
    /// </summary>
    public InterfaceFileStrategy InterfaceFileStrategy { get; set; } = InterfaceFileStrategy.SameFile;
    
    /// <summary>
    /// Модификатор доступа для классов HTTP клиентов
    /// </summary>
    public AccessModifier ClientAccessModifier { get; set; } = AccessModifier.Public;
    
    /// <summary>
    /// Генерировать traces (spans, activities) для OpenTelemetry
    /// </summary>
    public bool GenerateTraces { get; set; } = false;
    
    /// <summary>
    /// Генерировать metrics для OpenTelemetry
    /// </summary>
    public bool GenerateMetrics { get; set; } = false;
    
    /// <summary>
    /// Использовать nullable Activity (Activity?) вместо not nullable (Activity)
    /// Если true, то _activitySource.StartActivity возвращает Activity?
    /// Если false, то _activitySource.StartActivity возвращает Activity
    /// </summary>
    public bool NullableActivity { get; set; } = true;
    
    /// <summary>
    /// Название сервиса для ActivitySource и метрик
    /// Используется для префикса метрик (например, "myapi.requests.success") и для ActivitySource
    /// Если не указано, используется значение по умолчанию
    /// </summary>
    public string? ServiceName { get; set; }
}

/// <summary>
/// Тип аксессора свойства
/// </summary>
public enum PropertyAccessorType
{
    /// <summary>
    /// get; set;
    /// </summary>
    Set,

    /// <summary>
    /// get; init;
    /// </summary>
    Init
}

/// <summary>
/// Тип модели
/// </summary>
public enum ModelType
{
    /// <summary>
    /// class
    /// </summary>
    Class,

    /// <summary>
    /// record
    /// </summary>
    Record,

    /// <summary>
    /// struct
    /// </summary>
    Struct,

    /// <summary>
    /// record struct
    /// </summary>
    RecordStruct
}

/// <summary>
/// Стиль передачи query параметров
/// </summary>
public enum QueryParametersStyle
{
    /// <summary>
    /// Передавать query параметры как отдельные аргументы метода
    /// </summary>
    IndividualParameters,

    /// <summary>
    /// Передавать query параметры в виде класса (модели)
    /// </summary>
    ModelClass
}

/// <summary>
/// Стратегия размещения интерфейсов
/// </summary>
public enum InterfaceFileStrategy
{
    /// <summary>
    /// Генерировать интерфейсы в том же файле, что и реализация
    /// </summary>
    SameFile,

    /// <summary>
    /// Генерировать интерфейсы в отдельном файле
    /// </summary>
    SeparateFile
}
