
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
    
    /// <summary>
    /// Имя базового класса (для allOf - наследование)
    /// </summary>
    public string? BaseClassName { get; set; }
    
    public IOpenApiSchema? OriginalSchema { get; set; }
    
    /// <summary>
    /// Словарь оригинальных схем свойств (имя свойства -> схема свойства)
    /// Включает свойства из allOf и из самой схемы
    /// </summary>
    public IReadOnlyDictionary<string, IOpenApiSchema>? PropertySchemas { get; set; }
}

