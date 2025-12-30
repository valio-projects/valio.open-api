namespace ValIO.Generators.CSharp.Models;

/// <summary>
/// Данные для шаблона enum C#
/// </summary>
public class EnumTemplateData
{
    public string Namespace { get; set; } = string.Empty;
    public string EnumName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string UnderlyingType { get; set; } = "int";
    public IReadOnlyList<EnumValueTemplateData> Values { get; set; } = Array.Empty<EnumValueTemplateData>();
}

/// <summary>
/// Данные для шаблона значения enum
/// </summary>
public class EnumValueTemplateData
{
    public string Name { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string? Description { get; set; }
    public string ValueString { get; set; } = string.Empty;
}
