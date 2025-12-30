using System.Net.Http;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;

namespace ValIO.Core.Services;

/// <summary>
/// Реализация извлечения операций из OpenAPI документа
/// </summary>
public class OperationExtractor : IOperationExtractor
{
    private readonly ISchemaResolver _schemaResolver;

    public OperationExtractor(ISchemaResolver schemaResolver)
    {
        _schemaResolver = schemaResolver;
    }

    public IReadOnlyList<ApiOperation> ExtractOperations(OpenApiDocument document)
    {
        var operations = new List<ApiOperation>();

        if (document.Paths == null)
            return operations;

        foreach (var pathEntry in document.Paths)
        {
            var path = pathEntry.Key;
            var pathItem = pathEntry.Value;

            // Извлекаем операции для каждого HTTP метода
            if (pathItem.Operations != null)
            {
                foreach (var operationEntry in pathItem.Operations)
                {
                    var apiOperation = ExtractOperation(pathItem, operationEntry.Key, path);
                    operations.Add(apiOperation);
                }
            }
        }

        return operations;
    }

    public ApiOperation ExtractOperation(IOpenApiPathItem pathItem, HttpMethod operationType, string path)
    {
        if (pathItem.Operations == null || !pathItem.Operations.TryGetValue(operationType, out var openApiOperation) || openApiOperation == null)
            throw new ArgumentException($"Operation {operationType.Method} not found for path {path}");

        var operationId = openApiOperation.OperationId ?? $"{operationType.Method.ToLower()}{path.Replace("/", "").Replace("{", "").Replace("}", "")}";

        var apiOperation = new ApiOperation
        {
            OperationId = operationId,
            Path = path,
            HttpMethod = operationType,
            Summary = openApiOperation.Summary,
            Description = openApiOperation.Description,
            Tags = openApiOperation.Tags?.Select(t => t.Name).Where(n => n != null).Cast<string>().ToList() ?? new List<string>(),
            Parameters = ExtractParameters(openApiOperation.Parameters),
            RequestBody = ExtractRequestBody(openApiOperation.RequestBody),
            Responses = ExtractResponses(openApiOperation.Responses)
        };

        return apiOperation;
    }

    private IReadOnlyList<ApiParameter> ExtractParameters(IList<IOpenApiParameter>? parameters)
    {
        if (parameters == null)
            return Array.Empty<ApiParameter>();

        var apiParameters = new List<ApiParameter>();
        foreach (var parameter in parameters)
        {
            var resolvedSchema = parameter.Schema != null ? _schemaResolver.ResolveSchema(parameter.Schema) : null;
            var parameterType = resolvedSchema != null ? "object" : "string"; // Будет определено TypeMapper'ом

            var location = parameter.In ?? Microsoft.OpenApi.ParameterLocation.Query;
            apiParameters.Add(new ApiParameter
            {
                Name = parameter.Name ?? string.Empty,
                Location = MapParameterLocation(location),
                Type = parameterType,
                IsRequired = parameter.Required,
                Description = parameter.Description
            });
        }

        return apiParameters;
    }

    private ApiRequestBody? ExtractRequestBody(IOpenApiRequestBody? requestBody)
    {
        if (requestBody == null)
            return null;

        // Берем первый content type и схему
        var content = requestBody.Content?.FirstOrDefault();
        var schema = content.HasValue ? content.Value.Value?.Schema : null;
        var resolvedSchema = schema != null ? _schemaResolver.ResolveSchema(schema) : null;
        var contentType = content.HasValue ? content.Value.Key : "application/json";
        var requestBodyType = resolvedSchema != null ? "object" : "object"; // Будет определено TypeMapper'ом

        return new ApiRequestBody
        {
            ContentType = contentType,
            Type = requestBodyType,
            IsRequired = requestBody.Required,
            Description = requestBody.Description
        };
    }

    private IReadOnlyDictionary<string, ApiResponse> ExtractResponses(IDictionary<string, IOpenApiResponse>? responses)
    {
        if (responses == null)
            return new Dictionary<string, ApiResponse>();

        var apiResponses = new Dictionary<string, ApiResponse>();
        foreach (var responseEntry in responses)
        {
            var statusCode = responseEntry.Key;
            var response = responseEntry.Value;

            var content = response.Content?.FirstOrDefault();
            var schema = content.HasValue ? content.Value.Value?.Schema : null;
            var resolvedSchema = schema != null ? _schemaResolver.ResolveSchema(schema) : null;
            var responseType = resolvedSchema != null ? "object" : null; // Будет определено TypeMapper'ом

            apiResponses[statusCode] = new ApiResponse
            {
                StatusCode = statusCode,
                Description = response.Description,
                Type = responseType
            };
        }

        return apiResponses;
    }

    private static Models.ParameterLocation MapParameterLocation(Microsoft.OpenApi.ParameterLocation location)
    {
        return location switch
        {
            Microsoft.OpenApi.ParameterLocation.Query => Models.ParameterLocation.Query,
            Microsoft.OpenApi.ParameterLocation.Path => Models.ParameterLocation.Path,
            Microsoft.OpenApi.ParameterLocation.Header => Models.ParameterLocation.Header,
            Microsoft.OpenApi.ParameterLocation.Cookie => Models.ParameterLocation.Cookie,
            _ => Models.ParameterLocation.Query
        };
    }
}

