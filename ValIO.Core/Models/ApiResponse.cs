namespace ValIO.Core.Models;

/// <summary>
/// Ответ API
/// </summary>
public class ApiResponse
{
    public string StatusCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Type { get; set; }
}

