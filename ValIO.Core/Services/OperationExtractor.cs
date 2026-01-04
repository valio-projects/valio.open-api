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
        _schemaResolver = schemaResolver ?? throw new ArgumentNullException(nameof(schemaResolver));
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

    public IReadOnlyList<OperationTemplateData> ExtractOperationTemplateData(
        OpenApiDocument document, 
        ITypeMapper typeMapper, 
        INamingConverter namingConverter, 
        ITypeInfoBuilder typeInfoBuilder, 
        IDependencyAnalyzer dependencyAnalyzer, 
        IOperationNamingStrategy namingStrategy)
    {
        var operations = new List<OperationTemplateData>(16 * 1024);

        if (document.Paths == null)
            return operations;

        var dependencyCollector = new DependencyCollector(dependencyAnalyzer);

        foreach (var pathEntry in document.Paths)
        {
            var path = pathEntry.Key;
            var pathItem = pathEntry.Value;

            // Извлекаем операции для каждого HTTP метода
            if (pathItem.Operations != null)
            {
                foreach (var operationEntry in pathItem.Operations)
                {
                    var operationTemplateData = ExtractOperationTemplateData(
                        pathItem, 
                        operationEntry.Key, 
                        path, 
                        typeMapper, 
                        namingConverter, 
                        typeInfoBuilder, 
                        dependencyCollector, 
                        dependencyAnalyzer, 
                        namingStrategy);
                    operations.Add(operationTemplateData);
                }
            }
        }

        return operations;
    }

    public OperationTemplateData ExtractOperationTemplateData(
        IOpenApiPathItem pathItem, 
        HttpMethod operationType, 
        string path, 
        ITypeMapper typeMapper, 
        INamingConverter namingConverter, 
        ITypeInfoBuilder typeInfoBuilder, 
        IDependencyAnalyzer dependencyAnalyzer, 
        IOperationNamingStrategy namingStrategy)
    {
        var dependencyCollector = new DependencyCollector(dependencyAnalyzer);
        return ExtractOperationTemplateData(
            pathItem, 
            operationType, 
            path, 
            typeMapper, 
            namingConverter, 
            typeInfoBuilder, 
            dependencyCollector, 
            dependencyAnalyzer, 
            namingStrategy);
    }

    private OperationTemplateData ExtractOperationTemplateData(
        IOpenApiPathItem pathItem, 
        HttpMethod operationType, 
        string path, 
        ITypeMapper typeMapper, 
        INamingConverter namingConverter, 
        ITypeInfoBuilder typeInfoBuilder, 
        DependencyCollector dependencyCollector, 
        IDependencyAnalyzer dependencyAnalyzer, 
        IOperationNamingStrategy namingStrategy)
    {
        if (pathItem.Operations == null || !pathItem.Operations.TryGetValue(operationType, out var openApiOperation) || openApiOperation == null)
            throw new ArgumentException($"Operation {operationType.Method} not found for path {path}");

        var operationId = openApiOperation.OperationId ?? $"{operationType.Method.ToLower()}{path.Replace("/", "").Replace("{", "").Replace("}", "")}";
        var methodName = namingConverter.ToPascalCase(operationId);

        // Определяем язык из TypeInfoBuilder
        var language = typeInfoBuilder.Language;

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
            Responses = new List<ResponseTemplateData>(8),
            HasRequestBody = false,
            IsRequestBodyRequired = false // Инициализируем явно, будет установлено ниже если есть request body
        };

        // Извлекаем параметры
        if (openApiOperation.Parameters != null)
        {
            var pathParams = new List<ParameterTemplateData>(8);
            var queryParams = new List<ParameterTemplateData>(8);
            var headerParams = new List<ParameterTemplateData>(8);

            foreach (var parameter in openApiOperation.Parameters)
            {
                var paramTemplateData = ExtractParameterTemplateData(parameter, typeInfoBuilder, namingConverter, namingStrategy, language);
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
            var requestBodyData = ExtractRequestBodyTemplateData(openApiOperation.RequestBody, typeInfoBuilder, namingConverter, namingStrategy, language);
            operationTemplateData.HasRequestBody = true;
            
            // Определяем, является ли request body обязательным
            // Если явно указано Required = true, используем это значение
            // Если не указано (Required = false по умолчанию), но есть request body,
            // то для POST/PUT/PATCH операций считаем request body обязательным
            var isExplicitlyRequired = openApiOperation.RequestBody.Required;
            
            // Проверяем HTTP метод через Method property для корректного сравнения
            var httpMethod = operationType.Method.ToUpperInvariant();
            var isImplicitlyRequired = !isExplicitlyRequired && 
                                       (httpMethod == "POST" || 
                                        httpMethod == "PUT" || 
                                        httpMethod == "PATCH");
            
            operationTemplateData.IsRequestBodyRequired = isExplicitlyRequired || isImplicitlyRequired;
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
                    // Важно: передаем исходную schema (со ссылкой), а не разрешенную
                    // TypeInfoBuilder сам разрешит ссылку и правильно определит тип
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

    private ParameterTemplateData ExtractParameterTemplateData(
        IOpenApiParameter parameter, 
        ITypeInfoBuilder typeInfoBuilder, 
        INamingConverter namingConverter, 
        IOperationNamingStrategy namingStrategy,
        GenerationLanguage language)
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
            // Используем стратегию для получения типа по умолчанию
            var defaultType = namingStrategy.GetDefaultParameterType();
            typeInfo = TypeInfo.Simple(defaultType, isPrimitive: true, language: language);
        }

        // Используем стратегию для именования
        var variableName = namingStrategy.ToVariableName(parameterName, namingConverter);
        var propertyName = namingStrategy.ToPropertyName(parameterName, namingConverter);

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
        ITypeInfoBuilder typeInfoBuilder, 
        INamingConverter namingConverter,
        IOperationNamingStrategy namingStrategy,
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
                formDataProperties = ExtractFormDataPropertiesTemplateData(resolvedSchema, typeInfoBuilder, namingConverter, namingStrategy, language);
            }
        }
        else
        {
            // Используем стратегию для получения типа по умолчанию
            var defaultType = namingStrategy.GetDefaultRequestBodyType();
            typeInfo = TypeInfo.Simple(defaultType, language: language);
        }

        return (typeInfo, contentType, isFormData, formDataProperties);
    }

    private IReadOnlyList<FormDataPropertyTemplateData> ExtractFormDataPropertiesTemplateData(
        IOpenApiSchema schema, 
        ITypeInfoBuilder typeInfoBuilder, 
        INamingConverter namingConverter,
        IOperationNamingStrategy namingStrategy,
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

            // Используем стратегию для именования
            var variableName = namingStrategy.ToVariableName(propertyName, namingConverter);

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
