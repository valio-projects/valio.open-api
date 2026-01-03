using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Базовый интерфейс для данных шаблона enum (без generic параметров для renderers)
/// </summary>
public interface IEnumTemplateData
{
    string Name { get; set; }
    string? Description { get; set; }
    IReadOnlyList<EnumValueTemplateData> Values { get; }
    GenerationLanguage Language { get; set; }
    EnumFormat Format { get; set; }
    string? UnderlyingType { get; set; }
    string? Namespace { get; set; }
    string? ImportPath { get; set; }
}
