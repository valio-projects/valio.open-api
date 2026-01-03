using System.Net.Http;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

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

    public IReadOnlyList<OperationTemplateData> ExtractOperationTemplateData(OpenApiDocument document, GenerationLanguage language, ITypeMapper typeMapper, INamingConverter namingConverter)
    {
        var operations = new List<OperationTemplateData>(16 * 1024);

        if (document.Paths == null)
            return operations;

        var typeInfoBuilder = new TypeInfoBuilder(typeMapper, _schemaResolver, language);
        var dependencyCollector = new DependencyCollector(new DependencyAnalyzer(namingConverter));

        foreach (var pathEntry in document.Paths)
        {
            var path = pathEntry.Key;
            var pathItem = pathEntry.Value;

            // Извлекаем операции для каждого HTTP метода
            if (pathItem.Operations != null)
            {
                foreach (var operationEntry in pathItem.Operations)
                {
                    var operationTemplateData = ExtractOperationTemplateData(pathItem, operationEntry.Key, path, language, typeMapper, namingConverter, typeInfoBuilder, dependencyCollector);
                    operations.Add(operationTemplateData);
                }
            }
        }

        return operations;
    }

    public OperationTemplateData ExtractOperationTemplateData(IOpenApiPathItem pathItem, HttpMethod operationType, string path, GenerationLanguage language, ITypeMapper typeMapper, INamingConverter namingConverter)
    {
        return ExtractOperationTemplateData(pathItem, operationType, path, language, typeMapper, namingConverter, null, null);
    }

    private OperationTemplateData ExtractOperationTemplateData(IOpenApiPathItem pathItem, HttpMethod operationType, string path, GenerationLanguage language, ITypeMapper typeMapper, INamingConverter namingConverter, TypeInfoBuilder? typeInfoBuilder, DependencyCollector? dependencyCollector)
    {
        if (pathItem.Operations == null || !pathItem.Operations.TryGetValue(operationType, out var openApiOperation) || openApiOperation == null)
            throw new ArgumentException($"Operation {operationType.Method} not found for path {path}");

        typeInfoBuilder ??= new TypeInfoBuilder(typeMapper, _schemaResolver, language);
        dependencyCollector ??= new DependencyCollector(new DependencyAnalyzer(namingConverter));

        var operationId = openApiOperation.OperationId ?? $"{operationType.Method.ToLower()}{path.Replace("/", "").Replace("{", "").Replace("}", "")}";
        var methodName = namingConverter.ToPascalCase(operationId);

        var operationTemplateData = new OperationTemplateData
        {
            MethodName = methodName,
            HttpMethod = operationType.Method.ToUpperInvariant(),
            Path = path,
            Summary = openApiOperation.Summary,
            Description = openApiOperation.Description,
            Tags = openApiOperation.Tags?.Select(t => t.Name).Where(n => n != null).Cast<string>().ToList(),
            Language = language,
            PathParameters = new List<ParameterTemplateData>(8),
            QueryParameters = new List<ParameterTemplateData>(8),
            HeaderParameters = new List<ParameterTemplateData>(8),
            Responses = new List<ResponseTemplateData>(8)
        };

        // Извлекаем параметры
        if (openApiOperation.Parameters != null)
        {
            var pathParams = new List<ParameterTemplateData>(8);
            var queryParams = new List<ParameterTemplateData>(8);
            var headerParams = new List<ParameterTemplateData>(8);

            foreach (var parameter in openApiOperation.Parameters)
            {
                var paramTemplateData = ExtractParameterTemplateData(parameter, typeInfoBuilder, namingConverter, language);
                var location = parameter.In ?? Microsoft.OpenApi.ParameterLocation.Query;
                
                switch (location)
                {
                    case Microsoft.OpenApi.ParameterLocation.Path:
                        pathParams.Add(paramTemplateData);
                        break;
                    case Microsoft.OpenApi.ParameterLocation.Query:
                        queryParams.Add(paramTemplateData);
                        break;
                    case Microsoft.OpenApi.ParameterLocation.Header:
                        headerParams.Add(paramTemplateData);
                        break;
                }
            }

            operationTemplateData.PathParameters = pathParams;
            operationTemplateData.QueryParameters = queryParams;
            operationTemplateData.HeaderParameters = headerParams;
        }

        // Извлекаем request body
        if (openApiOperation.RequestBody != null)
        {
            var requestBodyData = ExtractRequestBodyTemplateData(openApiOperation.RequestBody, typeInfoBuilder, namingConverter, language);
            operationTemplateData.HasRequestBody = true;
            operationTemplateData.RequestBodyParameter = "request";
            operationTemplateData.RequestBodyTypeInfo = requestBodyData.TypeInfo;
            operationTemplateData.RequestBodyContentType = requestBodyData.ContentType;
            operationTemplateData.IsFormData = requestBodyData.IsFormData;
            operationTemplateData.FormDataProperties = requestBodyData.FormDataProperties;
        }

        // Извлекаем responses
        if (openApiOperation.Responses != null)
        {
            var responses = new List<ResponseTemplateData>(8);
            TypeInfo? successResponseType = null;

            foreach (var responseEntry in openApiOperation.Responses)
            {
                var statusCode = responseEntry.Key;
                var response = responseEntry.Value;
                var isSuccess = int.TryParse(statusCode, out var code) && code >= 200 && code < 300;

                var content = response.Content?.FirstOrDefault();
                var schema = content.HasValue ? content.Value.Value?.Schema : null;
                TypeInfo? responseTypeInfo = null;

                if (schema != null)
                {
                    var isNullable = false; // Responses обычно не nullable
                    responseTypeInfo = typeInfoBuilder.BuildFromSchema(schema, isNullable);
                }
                else
                {
                    responseTypeInfo = TypeInfo.Void(language);
                }

                if (isSuccess && successResponseType == null)
                {
                    successResponseType = responseTypeInfo;
                }

                responses.Add(new ResponseTemplateData
                {
                    StatusCode = statusCode,
                    Description = response.Description,
                    ResponseTypeInfo = responseTypeInfo,
                    IsSuccess = isSuccess
                });
            }

            operationTemplateData.Responses = responses;
            operationTemplateData.ReturnTypeInfo = successResponseType ?? TypeInfo.Void(language);
            operationTemplateData.HasResponseType = successResponseType != null && successResponseType.BaseType != "void";
        }
        else
        {
            operationTemplateData.ReturnTypeInfo = TypeInfo.Void(language);
            operationTemplateData.HasResponseType = false;
        }

        // Собираем зависимости
        var allDependencies = new HashSet<string>(8);
        
        // Зависимости из параметров
        foreach (var param in operationTemplateData.PathParameters.Concat(operationTemplateData.QueryParameters).Concat(operationTemplateData.HeaderParameters))
        {
            var paramDependencies = dependencyCollector.CollectFromTypeInfo(param.TypeInfo);
            foreach (var dep in paramDependencies)
            {
                allDependencies.Add(dep);
            }
        }

        // Зависимости из request body
        if (operationTemplateData.RequestBodyTypeInfo != null)
        {
            var requestBodyDependencies = dependencyCollector.CollectFromTypeInfo(operationTemplateData.RequestBodyTypeInfo);
            foreach (var dep in requestBodyDependencies)
            {
                allDependencies.Add(dep);
            }
        }

        // Зависимости из form-data properties
        if (operationTemplateData.FormDataProperties != null)
        {
            foreach (var formProp in operationTemplateData.FormDataProperties)
            {
                var formPropDependencies = dependencyCollector.CollectFromTypeInfo(formProp.TypeInfo);
                foreach (var dep in formPropDependencies)
                {
                    allDependencies.Add(dep);
                }
            }
        }

        // Зависимости из responses
        foreach (var response in operationTemplateData.Responses)
        {
            if (response.ResponseTypeInfo != null)
            {
                var responseDependencies = dependencyCollector.CollectFromTypeInfo(response.ResponseTypeInfo);
                foreach (var dep in responseDependencies)
                {
                    allDependencies.Add(dep);
                }
            }
        }

        operationTemplateData.Dependencies = allDependencies.ToList();

        return operationTemplateData;
    }

    private ParameterTemplateData ExtractParameterTemplateData(IOpenApiParameter parameter, TypeInfoBuilder typeInfoBuilder, INamingConverter namingConverter, GenerationLanguage language)
    {
        var parameterName = parameter.Name ?? string.Empty;
        var isRequired = parameter.Required;
        var location = parameter.In ?? Microsoft.OpenApi.ParameterLocation.Query;

        TypeInfo typeInfo;
        if (parameter.Schema != null)
        {
            var isNullable = !isRequired; // Параметры nullable, если не required
            typeInfo = typeInfoBuilder.BuildFromSchema(parameter.Schema, isNullable);
        }
        else
        {
            // По умолчанию string для параметров без схемы
            typeInfo = TypeInfo.Simple(language == GenerationLanguage.TypeScript ? "string" : "string", isPrimitive: true, language: language);
        }

        // Определяем имена переменных в зависимости от языка
        var variableName = language == GenerationLanguage.TypeScript 
            ? namingConverter.ToCamelCase(parameterName)
            : namingConverter.ToPascalCase(parameterName);
        var propertyName = namingConverter.ToPascalCase(parameterName);

        return new ParameterTemplateData
        {
            Name = parameterName,
            VariableName = variableName,
            PropertyName = propertyName,
            TypeInfo = typeInfo,
            IsRequired = isRequired,
            Description = parameter.Description
        };
    }

    private (TypeInfo? TypeInfo, string? ContentType, bool IsFormData, IReadOnlyList<FormDataPropertyTemplateData>? FormDataProperties) ExtractRequestBodyTemplateData(
        IOpenApiRequestBody requestBody, 
        TypeInfoBuilder typeInfoBuilder, 
        INamingConverter namingConverter,
        GenerationLanguage language)
    {
        // Берем первый content type и схему
        var content = requestBody.Content?.FirstOrDefault();
        var schema = content.HasValue ? content.Value.Value?.Schema : null;
        var contentType = content.HasValue ? content.Value.Key : "application/json";
        var isFormData = contentType == "application/x-www-form-urlencoded" || contentType == "multipart/form-data";

        TypeInfo? typeInfo = null;
        IReadOnlyList<FormDataPropertyTemplateData>? formDataProperties = null;

        if (schema != null)
        {
            var isNullable = !requestBody.Required;
            typeInfo = typeInfoBuilder.BuildFromSchema(schema, isNullable);

            // Если это form-data, извлекаем свойства
            if (isFormData)
            {
                var resolvedSchema = _schemaResolver.ResolveSchema(schema);
                formDataProperties = ExtractFormDataPropertiesTemplateData(resolvedSchema, typeInfoBuilder, namingConverter, language);
            }
        }
        else
        {
            typeInfo = TypeInfo.Simple(language == GenerationLanguage.TypeScript ? "any" : "object", language: language);
        }

        return (typeInfo, contentType, isFormData, formDataProperties);
    }

    private IReadOnlyList<FormDataPropertyTemplateData> ExtractFormDataPropertiesTemplateData(
        IOpenApiSchema schema, 
        TypeInfoBuilder typeInfoBuilder, 
        INamingConverter namingConverter,
        GenerationLanguage language)
    {
        var properties = new List<FormDataPropertyTemplateData>(16);

        if (schema.Properties == null)
            return properties;

        foreach (var propertyEntry in schema.Properties)
        {
            var propertyName = propertyEntry.Key;
            var propertySchema = propertyEntry.Value;
            var resolvedPropertySchema = _schemaResolver.ResolveSchema(propertySchema);
            
            var isRequired = schema.Required?.Contains(propertyName) ?? false;
            var isNullable = !isRequired;
            var typeInfo = typeInfoBuilder.BuildFromSchema(propertySchema, isNullable);

            var variableName = language == GenerationLanguage.TypeScript 
                ? namingConverter.ToCamelCase(propertyName)
                : namingConverter.ToPascalCase(propertyName);

            properties.Add(new FormDataPropertyTemplateData
            {
                Name = propertyName,
                VariableName = variableName,
                TypeInfo = typeInfo,
                IsRequired = isRequired,
                Description = resolvedPropertySchema.Description
            });
        }

        return properties;
    }
}

