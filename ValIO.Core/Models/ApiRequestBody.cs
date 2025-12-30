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
    
    /// <summary>
    /// Свойства для form-data и multipart/form-data
    /// Ключ - имя свойства, значение - тип свойства
    /// </summary>
    public IReadOnlyDictionary<string, FormDataProperty>? FormDataProperties { get; set; }
    
    /// <summary>
    /// Является ли request body form-data (application/x-www-form-urlencoded или multipart/form-data)
    /// </summary>
    public bool IsFormData => ContentType == "application/x-www-form-urlencoded" || ContentType == "multipart/form-data";
}

/// <summary>
/// Свойство form-data
/// </summary>
public class FormDataProperty
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsFile { get; set; }
    public bool IsArray { get; set; }
    public string? Description { get; set; }
}

