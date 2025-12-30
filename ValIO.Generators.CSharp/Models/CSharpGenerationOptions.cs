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
