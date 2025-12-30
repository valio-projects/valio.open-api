using Microsoft.OpenApi.Models;
using ValIO.Core.Models;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для извлечения моделей из OpenAPI схемы
/// </summary>
public interface IModelExtractor
{
    IReadOnlyList<ApiModel> ExtractModels(OpenApiDocument document);
    ApiModel ExtractModel(OpenApiSchema schema, string modelName);
}

