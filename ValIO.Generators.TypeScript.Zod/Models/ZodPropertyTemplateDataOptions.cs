using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Generators.TypeScript.Zod.Models;

/// <summary>
/// Zod специфичные опции для PropertyTemplateData
/// </summary>
public class ZodPropertyTemplateDataOptions : PropertyTemplateDataOptions
{
    /// <summary>
    /// Zod схема для свойства (строка)
    /// </summary>
    public string ZodSchema { get; set; } = "z.any()";
}
