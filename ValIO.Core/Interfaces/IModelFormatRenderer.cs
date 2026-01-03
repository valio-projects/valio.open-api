using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для рендеринга моделей в разных форматах (базовый, без generic параметров)
/// </summary>
public interface IModelFormatRenderer
{
    /// <summary>
    /// Формат модели, который поддерживает этот renderer
    /// </summary>
    ModelFormat Format { get; }
    
    /// <summary>
    /// Язык генерации, который поддерживает этот renderer
    /// </summary>
    GenerationLanguage Language { get; }
    
    /// <summary>
    /// Рендерит модель в указанном формате
    /// </summary>
    string Render(IModelTemplateData data);
    
    /// <summary>
    /// Рендерит модель в указанном формате с дополнительными параметрами для создания правильной модели
    /// </summary>
    string Render(IModelTemplateData data, OpenApiDocument document, ISchemaResolver schemaResolver);
}

/// <summary>
/// Generic интерфейс для рендеринга моделей в разных форматах со строгой типизацией
/// </summary>
/// <typeparam name="TOptions">Тип опций модели (язык-специфичный)</typeparam>
/// <typeparam name="TPropertyOptions">Тип опций свойств (язык-специфичный)</typeparam>
public interface IModelFormatRenderer<TOptions, TPropertyOptions> : IModelFormatRenderer
    where TOptions : ModelTemplateDataOptions, new()
    where TPropertyOptions : PropertyTemplateDataOptions, new()
{
    /// <summary>
    /// Рендерит модель в указанном формате со строгой типизацией
    /// </summary>
    string Render(ModelTemplateData<TOptions, TPropertyOptions> data);
}
