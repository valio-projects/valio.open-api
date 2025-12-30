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
}

