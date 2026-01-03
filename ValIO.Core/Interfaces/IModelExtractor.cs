using Microsoft.OpenApi;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для извлечения моделей из OpenAPI схемы
/// </summary>
public interface IModelExtractor
{
    /// <summary>
    /// Извлекает модели в виде TemplateData с уже подготовленными данными и зависимостями
    /// </summary>
    IReadOnlyList<ModelTemplateData> ExtractModelTemplateData(OpenApiDocument document, GenerationLanguage language, ITypeMapper typeMapper);
    
    /// <summary>
    /// Извлекает модель в виде TemplateData
    /// </summary>
    ModelTemplateData ExtractModelTemplateData(IOpenApiSchema schema, string modelName, GenerationLanguage language, ITypeMapper typeMapper);
}

