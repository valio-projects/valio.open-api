
using Microsoft.OpenApi;

namespace ValIO.Core.Models;

/// <summary>
/// Модель данных API
/// </summary>
/// <remarks>
/// Устаревший класс. Используйте ModelTemplateData вместо этого.
/// </remarks>
[Obsolete("Use ModelTemplateData instead. This class will be removed in a future version.")]
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
    
    /// <summary>
    /// Множество имен свойств, которые пришли из базовой схемы (allOf)
    /// Используется для разделения свойств базовой схемы и собственных свойств
    /// </summary>
    public IReadOnlySet<string>? BaseClassProperties { get; set; }
}

