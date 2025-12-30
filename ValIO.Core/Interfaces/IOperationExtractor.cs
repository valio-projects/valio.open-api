using Microsoft.OpenApi.Models;
using ValIO.Core.Models;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для извлечения операций из OpenAPI схемы
/// </summary>
public interface IOperationExtractor
{
    IReadOnlyList<ApiOperation> ExtractOperations(OpenApiDocument document);
    ApiOperation ExtractOperation(OpenApiPathItem pathItem, OperationType operationType, string path);
}

