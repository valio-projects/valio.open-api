using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Generators.TypeScript.Models;

/// <summary>
/// TypeScript специфичные опции для ModelTemplateData
/// </summary>
public class TypeScriptModelTemplateDataOptions : ModelTemplateDataOptions
{
    /// <summary>
    /// Библиотека валидации (для генерации Zod схем и т.д.)
    /// </summary>
    public ValidationLibrary? ValidationLibrary { get; set; }
}
