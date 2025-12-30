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
}

/// <summary>
/// Данные для шаблона свойства
/// </summary>
public class PropertyTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
}

