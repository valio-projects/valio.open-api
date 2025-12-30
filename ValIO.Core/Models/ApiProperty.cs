namespace ValIO.Core.Models;

/// <summary>
/// Свойство модели API
/// </summary>
public class ApiProperty
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsNullable { get; set; }
    public string? Description { get; set; }
    public object? DefaultValue { get; set; }
}

