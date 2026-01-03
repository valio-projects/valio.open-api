using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Services;

namespace ValIO.Generators.TypeScript.Services;

/// <summary>
/// Извлекатель enum для TypeScript
/// </summary>
public class TypeScriptEnumExtractor : BaseEnumExtractor
{
    public TypeScriptEnumExtractor(ValIO.Core.Interfaces.ISchemaResolver schemaResolver, ValIO.Core.Interfaces.INamingConverter namingConverter)
        : base(schemaResolver, namingConverter, GenerationLanguage.TypeScript)
    {
    }

    /// <summary>
    /// Определяет формат enum для TypeScript (Union для string, Enum для остальных)
    /// </summary>
    protected override EnumFormat DetermineEnumFormat(string underlyingType)
    {
        return underlyingType == "string" ? EnumFormat.Union : EnumFormat.Enum;
    }
}
