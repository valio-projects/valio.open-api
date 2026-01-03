using Microsoft.OpenApi;

namespace ValIO.Core.Models;

/// <summary>
/// Enum из OpenAPI схемы
/// </summary>
/// <remarks>
/// Устаревший класс. Используйте EnumTemplateData вместо этого.
/// </remarks>
[Obsolete("Use EnumTemplateData instead. This class will be removed in a future version.")]
public class ApiEnum
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<ApiEnumValue> Values { get; set; } = Array.Empty<ApiEnumValue>();
    public string UnderlyingType { get; set; } = "int";
    public IOpenApiSchema? OriginalSchema { get; set; }
}

/// <summary>
/// Значение enum
/// </summary>
public class ApiEnumValue
{
    public string Name { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string? Description { get; set; }
}
