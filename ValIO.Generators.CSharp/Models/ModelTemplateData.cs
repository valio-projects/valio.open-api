namespace ValIO.Generators.CSharp.Models;

/// <summary>
/// Данные для шаблона модели C#
/// </summary>
public class ModelTemplateData
{
    public string Namespace { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<PropertyTemplateData> Properties { get; set; } = Array.Empty<PropertyTemplateData>();
    
    /// <summary>
    /// Тип модели: class, record, struct, record struct
    /// </summary>
    public ModelType ModelType { get; set; } = ModelType.Class;
    
    /// <summary>
    /// Делать класс sealed
    /// </summary>
    public bool IsSealed { get; set; } = false;
    
    /// <summary>
    /// Генерировать методы сравнения (для record)
    /// </summary>
    public bool GenerateEqualityMembers { get; set; } = true;
    
    /// <summary>
    /// Генерировать статические классы с константами валидации
    /// </summary>
    public bool GenerateValidationConstants { get; set; } = false;

    /// <summary>
    /// Генерировать атрибуты валидации для свойств
    /// </summary>
    public bool GenerateValidationAttributes { get; set; } = false;

    /// <summary>
    /// Нужен ли using System.ComponentModel.DataAnnotations
    /// </summary>
    public bool NeedsDataAnnotations { get; set; } = false;

    /// <summary>
    /// Нужен ли using System.Text.RegularExpressions
    /// </summary>
    public bool NeedsRegularExpressions { get; set; } = false;
}

/// <summary>
/// Данные для шаблона свойства
/// </summary>
public class PropertyTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    /// <summary>
    /// Тип аксессора: set или init
    /// </summary>
    public PropertyAccessorType AccessorType { get; set; } = PropertyAccessorType.Set;
    
    /// <summary>
    /// Использовать required модификатор
    /// </summary>
    public bool IsRequired { get; set; } = false;
    
    /// <summary>
    /// Свойство nullable (добавляется "?")
    /// </summary>
    public bool IsNullable { get; set; } = false;
    
    /// <summary>
    /// Значение по умолчанию
    /// </summary>
    public object? DefaultValue { get; set; }
    
    /// <summary>
    /// Минимальное значение (для чисел)
    /// </summary>
    public decimal? Min { get; set; }
    
    /// <summary>
    /// Максимальное значение (для чисел)
    /// </summary>
    public decimal? Max { get; set; }
    
    /// <summary>
    /// Минимальная длина (для строк)
    /// </summary>
    public int? MinLength { get; set; }
    
    /// <summary>
    /// Максимальная длина (для строк)
    /// </summary>
    public int? MaxLength { get; set; }
    
    /// <summary>
    /// Исключающее минимальное значение (для чисел, > вместо >=)
    /// </summary>
    public decimal? ExclusiveMin { get; set; }
    
    /// <summary>
    /// Исключающее максимальное значение (для чисел, < вместо <=)
    /// </summary>
    public decimal? ExclusiveMax { get; set; }
    
    /// <summary>
    /// Регулярное выражение для валидации (для строк)
    /// </summary>
    public string? Pattern { get; set; }
}

