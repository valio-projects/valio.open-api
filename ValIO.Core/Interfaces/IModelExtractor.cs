using Microsoft.OpenApi;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для извлечения моделей из OpenAPI схемы
/// </summary>
[Obsolete("Используется IrBuilder вместо ModelExtractor")]
public interface IModelExtractor
{
    /// <summary>
    /// Извлекает модели в виде TemplateData с уже подготовленными данными и зависимостями
    /// </summary>
    IReadOnlyList<ModelTemplateData> ExtractModelTemplateData(OpenApiDocument document, ITypeMapper typeMapper, ITypeInfoBuilder typeInfoBuilder, IDependencyAnalyzer dependencyAnalyzer);
    
    /// <summary>
    /// Извлекает модель в виде TemplateData
    /// </summary>
    ModelTemplateData ExtractModelTemplateData(IOpenApiSchema schema, string modelName, ITypeMapper typeMapper, ITypeInfoBuilder typeInfoBuilder, IDependencyAnalyzer dependencyAnalyzer);
}

