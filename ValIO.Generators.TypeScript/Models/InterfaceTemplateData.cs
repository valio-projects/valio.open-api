namespace ValIO.Generators.TypeScript.Models;

/// <summary>
/// Данные для шаблона интерфейса TypeScript
/// </summary>
public class InterfaceTemplateData
{
    public string InterfaceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<TypeScriptPropertyTemplateData> Properties { get; set; } = Array.Empty<TypeScriptPropertyTemplateData>();
}

/// <summary>
/// Данные для шаблона свойства TypeScript
/// </summary>
public class TypeScriptPropertyTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Optional { get; set; } = "";
    public string? Description { get; set; }
}

