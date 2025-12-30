using Microsoft.OpenApi.Models;
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
            foreach (var operationType in Enum.GetValues<OperationType>())
            {
                var operation = pathItem.Operations?.FirstOrDefault(o => o.Key == operationType);
                if (operation.HasValue && operation.Value.Value != null)
                {
                    var apiOperation = ExtractOperation(pathItem, operationType, path);
                    operations.Add(apiOperation);
                }
            }
        }

        return operations;
    }

    public ApiOperation ExtractOperation(OpenApiPathItem pathItem, OperationType operationType, string path)
    {
        var operation = pathItem.Operations?.FirstOrDefault(o => o.Key == operationType);
        if (!operation.HasValue || operation.Value.Value == null)
            throw new ArgumentException($"Operation {operationType} not found for path {path}");

        var openApiOperation = operation.Value.Value;
        var operationId = openApiOperation.OperationId ?? $"{operationType.ToString().ToLower()}{path.Replace("/", "").Replace("{", "").Replace("}", "")}";

        var apiOperation = new ApiOperation
        {
            OperationId = operationId,
            Path = path,
            HttpMethod = operationType,
            Summary = openApiOperation.Summary,
            Description = openApiOperation.Description,
            Tags = openApiOperation.Tags?.Select(t => t.Name).ToList() ?? new List<string>(),
            Parameters = ExtractParameters(openApiOperation.Parameters),
            RequestBody = ExtractRequestBody(openApiOperation.RequestBody),
            Responses = ExtractResponses(openApiOperation.Responses)
        };

        return apiOperation;
    }

    private IReadOnlyList<ApiParameter> ExtractParameters(IList<OpenApiParameter>? parameters)
    {
        if (parameters == null)
            return Array.Empty<ApiParameter>();

        var apiParameters = new List<ApiParameter>();
        foreach (var parameter in parameters)
        {
            var resolvedSchema = parameter.Schema != null ? _schemaResolver.ResolveSchema(parameter.Schema) : null;
            var parameterType = resolvedSchema != null ? "object" : "string"; // Будет определено TypeMapper'ом

            apiParameters.Add(new ApiParameter
            {
                Name = parameter.Name,
                Location = MapParameterLocation(parameter.In),
                Type = parameterType,
                IsRequired = parameter.Required,
                Description = parameter.Description
            });
        }

        return apiParameters;
    }

    private ApiRequestBody? ExtractRequestBody(OpenApiRequestBody? requestBody)
    {
        if (requestBody == null)
            return null;

        // Берем первый content type и схему
        var content = requestBody.Content?.FirstOrDefault();
        var schema = content.Value.Value?.Schema;
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

    private IReadOnlyDictionary<string, ApiResponse> ExtractResponses(OpenApiResponses? responses)
    {
        if (responses == null)
            return new Dictionary<string, ApiResponse>();

        var apiResponses = new Dictionary<string, ApiResponse>();
        foreach (var responseEntry in responses)
        {
            var statusCode = responseEntry.Key;
            var response = responseEntry.Value;

            var content = response.Content?.FirstOrDefault();
            var schema = content.Value.Value?.Schema;
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

    private static ParameterLocation MapParameterLocation(ParameterLocation location)
    {
        return location switch
        {
            Microsoft.OpenApi.Models.ParameterLocation.Query => Models.ParameterLocation.Query,
            Microsoft.OpenApi.Models.ParameterLocation.Path => Models.ParameterLocation.Path,
            Microsoft.OpenApi.Models.ParameterLocation.Header => Models.ParameterLocation.Header,
            Microsoft.OpenApi.Models.ParameterLocation.Cookie => Models.ParameterLocation.Cookie,
            _ => Models.ParameterLocation.Query
        };
    }
}

