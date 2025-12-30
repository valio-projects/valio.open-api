using Microsoft.OpenApi;
using ValIO.Core.Models;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для извлечения enum из OpenAPI документа
/// </summary>
public interface IEnumExtractor
{
    /// <summary>
    /// Извлекает все enum из документа
    /// </summary>
    IReadOnlyList<ApiEnum> ExtractEnums(OpenApiDocument document);

    /// <summary>
    /// Извлекает enum из схемы
    /// </summary>
    ApiEnum? ExtractEnum(IOpenApiSchema schema, string enumName);
}
