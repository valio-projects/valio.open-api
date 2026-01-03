using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Generators.TypeScript.Models;

/// <summary>
/// TypeScript специфичные опции для EnumTemplateData
/// </summary>
public class TypeScriptEnumTemplateDataOptions : EnumTemplateDataOptions
{
    /// <summary>
    /// Библиотека валидации (для генерации Zod схем и т.д.)
    /// </summary>
    public ValidationLibrary? ValidationLibrary { get; set; }
}
