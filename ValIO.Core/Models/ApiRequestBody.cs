namespace ValIO.Core.Models;

/// <summary>
/// Тело запроса API
/// </summary>
public class ApiRequestBody
{
    public string? ContentType { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}

