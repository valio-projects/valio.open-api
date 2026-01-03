using Microsoft.OpenApi;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для извлечения enum из OpenAPI документа
/// </summary>
public interface IEnumExtractor
{
    /// <summary>
    /// Извлекает enum в виде TemplateData с уже подготовленными данными
    /// </summary>
    IReadOnlyList<EnumTemplateData> ExtractEnumTemplateData(OpenApiDocument document);
    
    /// <summary>
    /// Извлекает enum в виде TemplateData
    /// </summary>
    EnumTemplateData? ExtractEnumTemplateData(IOpenApiSchema schema, string enumName);
}
