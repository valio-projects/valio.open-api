using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Services;

namespace ValIO.Generators.CSharp.Services;

/// <summary>
/// Извлекатель enum для C#
/// </summary>
public class CSharpEnumExtractor : BaseEnumExtractor
{
    public CSharpEnumExtractor(ISchemaResolver schemaResolver, INamingConverter namingConverter)
        : base(schemaResolver, namingConverter, GenerationLanguage.CSharp)
    {
    }

    /// <summary>
    /// Определяет формат enum для C# (всегда Enum)
    /// </summary>
    protected override EnumFormat DetermineEnumFormat(string underlyingType)
    {
        return EnumFormat.Enum;
    }
}
