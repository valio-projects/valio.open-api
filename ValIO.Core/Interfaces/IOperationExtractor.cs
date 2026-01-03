using System.Net.Http;
using Microsoft.OpenApi;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для извлечения операций из OpenAPI схемы
/// </summary>
public interface IOperationExtractor
{
    /// <summary>
    /// Извлекает операции в виде TemplateData с уже подготовленными данными и зависимостями
    /// </summary>
    IReadOnlyList<OperationTemplateData> ExtractOperationTemplateData(OpenApiDocument document, GenerationLanguage language, ITypeMapper typeMapper, INamingConverter namingConverter);
    
    /// <summary>
    /// Извлекает операцию в виде TemplateData
    /// </summary>
    OperationTemplateData ExtractOperationTemplateData(IOpenApiPathItem pathItem, HttpMethod operationType, string path, GenerationLanguage language, ITypeMapper typeMapper, INamingConverter namingConverter);
}

