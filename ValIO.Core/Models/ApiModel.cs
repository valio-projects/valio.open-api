using Microsoft.OpenApi.Models;

namespace ValIO.Core.Models;

/// <summary>
/// Модель данных API
/// </summary>
public class ApiModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<ApiProperty> Properties { get; set; } = Array.Empty<ApiProperty>();
    public IReadOnlyList<string> RequiredProperties { get; set; } = Array.Empty<string>();
    public OpenApiSchema? OriginalSchema { get; set; }
}

