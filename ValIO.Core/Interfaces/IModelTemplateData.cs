using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Базовый интерфейс для данных шаблона модели (без generic параметров для renderers)
/// </summary>
public interface IModelTemplateData
{
    string Name { get; set; }
    string? Description { get; set; }
    IReadOnlyList<IPropertyTemplateData> Properties { get; }
    GenerationLanguage Language { get; set; }
    ModelFormat Format { get; set; }
    IReadOnlyList<string> Dependencies { get; set; }
    string? BaseClassName { get; set; }
    IReadOnlyList<IPropertyTemplateData> BaseClassProperties { get; }
    IReadOnlyList<IPropertyTemplateData> OwnProperties { get; }
    string? Namespace { get; set; }
    string? ImportPath { get; set; }
}

/// <summary>
/// Базовый интерфейс для данных шаблона свойства (без generic параметров для renderers)
/// </summary>
public interface IPropertyTemplateData
{
    string Name { get; set; }
    TypeInfo TypeInfo { get; set; }
    string? Description { get; set; }
    bool IsRequired { get; set; }
    bool IsNullable { get; set; }
    bool IsOptional { get; set; }
    object? DefaultValue { get; set; }
    decimal? Min { get; set; }
    decimal? Max { get; set; }
    int? MinLength { get; set; }
    int? MaxLength { get; set; }
    decimal? ExclusiveMin { get; set; }
    decimal? ExclusiveMax { get; set; }
    string? Pattern { get; set; }
}
