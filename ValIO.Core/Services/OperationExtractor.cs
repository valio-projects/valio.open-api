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

        var apiRequestBody = new ApiRequestBody
        {
            ContentType = contentType,
            Type = requestBodyType,
            IsRequired = requestBody.Required,
            Description = requestBody.Description
        };

        // Если это form-data, извлекаем свойства из схемы
        if (apiRequestBody.IsFormData && resolvedSchema != null)
        {
            apiRequestBody.FormDataProperties = ExtractFormDataProperties(resolvedSchema);
        }

        return apiRequestBody;
    }

    /// <summary>
    /// Извлекает свойства из схемы для form-data
    /// </summary>
    private IReadOnlyDictionary<string, FormDataProperty> ExtractFormDataProperties(IOpenApiSchema schema)
    {
        var properties = new Dictionary<string, FormDataProperty>();

        if (schema.Properties == null)
            return properties;

        foreach (var propertyEntry in schema.Properties)
        {
            var propertyName = propertyEntry.Key;
            var propertySchema = propertyEntry.Value;
            var resolvedPropertySchema = _schemaResolver.ResolveSchema(propertySchema);
            
            var isRequired = schema.Required?.Contains(propertyName) ?? false;
            var isFile = IsFileProperty(resolvedPropertySchema);
            var isArray = resolvedPropertySchema.Type.HasValue && 
                          resolvedPropertySchema.Type.Value.HasFlag(JsonSchemaType.Array);
            
            // Определяем тип свойства
            var propertyType = "string"; // По умолчанию для form-data
            if (isFile)
            {
                propertyType = isArray ? "List<Stream>" : "Stream";
            }
            else if (isArray)
            {
                var itemType = resolvedPropertySchema.Items != null 
                    ? MapFormDataPropertyType(_schemaResolver.ResolveSchema(resolvedPropertySchema.Items))
                    : "string";
                propertyType = $"List<{itemType}>";
            }
            else
            {
                propertyType = MapFormDataPropertyType(resolvedPropertySchema);
            }

            properties[propertyName] = new FormDataProperty
            {
                Name = propertyName,
                Type = propertyType,
                IsRequired = isRequired,
                IsFile = isFile,
                IsArray = isArray,
                Description = resolvedPropertySchema.Description
            };
        }

        return properties;
    }

    /// <summary>
    /// Проверяет, является ли свойство файлом
    /// </summary>
    private static bool IsFileProperty(IOpenApiSchema schema)
    {
        // Проверяем format: binary или base64
        if (schema.Format == "binary" || schema.Format == "base64")
            return true;

        // Проверяем тип string с format binary
        if (schema.Type.HasValue && 
            schema.Type.Value.HasFlag(JsonSchemaType.String) && 
            (schema.Format == "binary" || schema.Format == "base64"))
            return true;

        return false;
    }

    /// <summary>
    /// Маппит тип свойства для form-data
    /// </summary>
    private static string MapFormDataPropertyType(IOpenApiSchema schema)
    {
        if (!schema.Type.HasValue)
            return "string";

        var type = schema.Type.Value;
        var format = schema.Format?.ToLowerInvariant();

        // Убираем Null из типа
        var baseType = type & ~JsonSchemaType.Null;

        return (baseType, format) switch
        {
            (JsonSchemaType.Boolean, _) => "bool",
            (JsonSchemaType.Integer, "int32") => "int",
            (JsonSchemaType.Integer, "int64") => "long",
            (JsonSchemaType.Integer, _) => "int",
            (JsonSchemaType.Number, "float") => "float",
            (JsonSchemaType.Number, "double") => "double",
            (JsonSchemaType.Number, "decimal") => "decimal",
            (JsonSchemaType.Number, _) => "double",
            (JsonSchemaType.String, "date") => "DateTime",
            (JsonSchemaType.String, "date-time") => "DateTimeOffset",
            (JsonSchemaType.String, "uuid") => "Guid",
            (JsonSchemaType.String, "uri") => "Uri",
            (JsonSchemaType.String, "binary") => "Stream",
            (JsonSchemaType.String, _) => "string",
            _ => "string"
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

