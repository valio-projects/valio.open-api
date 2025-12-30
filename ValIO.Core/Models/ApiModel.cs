
using Microsoft.OpenApi;

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
    public IOpenApiSchema? OriginalSchema { get; set; }
}

