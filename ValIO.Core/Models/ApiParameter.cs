namespace ValIO.Core.Models;

/// <summary>
/// Параметр операции API
/// </summary>
public class ApiParameter
{
    public string Name { get; set; } = string.Empty;
    public ParameterLocation Location { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}

