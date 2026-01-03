using ValIO.Core.Models;

namespace ValIO.Generators.CSharp.Models;

/// <summary>
/// Данные для шаблона исключения
/// </summary>
public class ExceptionTemplateData
{
    public string Namespace { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public int StatusCodeNumber { get; set; }
    public string? Description { get; set; }
    
    /// <summary>
    /// Информация о типе ответа с мета-данными (если есть модель)
    /// </summary>
    public TypeInfo? ResponseTypeInfo { get; set; }
}
