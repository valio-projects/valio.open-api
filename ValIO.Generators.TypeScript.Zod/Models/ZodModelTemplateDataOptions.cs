using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Generators.TypeScript.Zod.Models;

/// <summary>
/// Zod специфичные опции для ModelTemplateData
/// </summary>
public class ZodModelTemplateDataOptions : ModelTemplateDataOptions
{
    /// <summary>
    /// Список зависимостей - схем, которые нужно импортировать
    /// </summary>
    public IReadOnlyList<string> SchemaDependencies { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Список зависимостей - enum схем, которые нужно импортировать
    /// </summary>
    public IReadOnlyList<string> EnumDependencies { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Флаг, указывающий, что схема содержит циклическую ссылку на саму себя
    /// </summary>
    public bool HasCircularReference { get; set; }
    
    /// <summary>
    /// Список зависимостей - TypeScript типов, которые нужно импортировать из .model.ts файлов (только для рекурсивных схем)
    /// </summary>
    public IReadOnlyList<string> TypeDependencies { get; set; } = Array.Empty<string>();
}
