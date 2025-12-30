using System.Net.Http;
using Microsoft.OpenApi;
using ValIO.Core.Models;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для извлечения операций из OpenAPI схемы
/// </summary>
public interface IOperationExtractor
{
    IReadOnlyList<ApiOperation> ExtractOperations(OpenApiDocument document);
    ApiOperation ExtractOperation(IOpenApiPathItem pathItem, HttpMethod operationType, string path);
}

