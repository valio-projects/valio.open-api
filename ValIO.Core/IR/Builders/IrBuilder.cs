using System.Collections.Immutable;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Core.IR.Builders;

/// <summary>
/// Построитель Language-agnostic IR из OpenAPI документа
/// </summary>
public class IrBuilder
{
    private readonly ISchemaResolver _schemaResolver;
    private readonly INamingConverter _namingConverter;
    
    // Словарь для хранения созданных inline моделей
    private readonly Dictionary<string, IrModel> _inlineModels = new();
    private int _inlineModelCounter = 0;
    
    public IrBuilder(ISchemaResolver schemaResolver, INamingConverter namingConverter)
    {
        _schemaResolver = schemaResolver ?? throw new ArgumentNullException(nameof(schemaResolver));
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }
    
    /// <summary>
    /// Строит IR из OpenAPI документа
    /// </summary>
    public IrDocument BuildFromOpenApi(OpenApiDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        
        // Очищаем словарь inline моделей перед началом
        _inlineModels.Clear();
        _inlineModelCounter = 0;
        
        var models = ExtractModels(document);
        var enums = ExtractEnums(document);
        var clients = ExtractClients(document);
        
        // Добавляем inline модели к основным моделям
        var allModels = models.Concat(_inlineModels.Values).ToList();
        
        var typeDependencies = BuildTypeDependencyGraph(allModels, enums, clients);
        
        return new IrDocument
        {
            Metadata = ExtractMetadata(document),
            Models = allModels,
            Enums = enums,
            Clients = clients,
            TypeDependencies = typeDependencies
        };
    }
    
    /// <summary>
    /// Извлекает метаданные из документа
    /// </summary>
    private IrDocumentMetadata ExtractMetadata(OpenApiDocument document)
    {
        return new IrDocumentMetadata
        {
            Title = document.Info?.Title,
            Version = document.Info?.Version,
            Description = document.Info?.Description,
            ServerUrl = document.Servers?.FirstOrDefault()?.Url
        };
    }
    
    /// <summary>
    /// Извлекает модели из компонентов
    /// </summary>
    private IReadOnlyList<IrModel> ExtractModels(OpenApiDocument document)
    {
        var models = new List<IrModel>();
        
        if (document.Components?.Schemas == null)
            return models;
        
        foreach (var (schemaName, schema) in document.Components.Schemas)
        {
            // Пропускаем enum схемы (они будут обработаны в ExtractEnums)
            // Enum должен иметь непустой список значений и тип string или integer, без Properties
            if (schema.Enum != null && schema.Enum.Count > 0)
            {
                if (schema.Type.HasValue)
                {
                    var type = schema.Type.Value;
                    var isStringEnum = type.HasFlag(JsonSchemaType.String) && !type.HasFlag(JsonSchemaType.Integer) && !type.HasFlag(JsonSchemaType.Number);
                    var isIntegerEnum = type.HasFlag(JsonSchemaType.Integer) && !type.HasFlag(JsonSchemaType.String) && !type.HasFlag(JsonSchemaType.Number);
                    
                    // Если это enum (string или integer) и нет Properties - пропускаем
                    if ((isStringEnum || isIntegerEnum) && (schema.Properties == null || schema.Properties.Count == 0))
                        continue;
                }
            }
            
            var sanitizedName = _namingConverter.SanitizeIdentifier(schemaName);
            var model = BuildModel(sanitizedName, schema, document);
            models.Add(model);
        }
        
        return models;
    }
    
    /// <summary>
    /// Строит модель из схемы
    /// </summary>
    private IrModel BuildModel(string name, IOpenApiSchema schema, OpenApiDocument document, string? parentModelName = null, PropertyContext? propertyContext = null)
    {
        var properties = ExtractProperties(schema, document, parentModelName);
        var composition = ExtractComposition(schema, document, propertyContext);
        
        return new IrModel
        {
            Name = name,
            Description = schema.Description,
            Properties = properties,
            Composition = composition,
            OriginalSchema = schema
        };
    }
    
    /// <summary>
    /// Извлекает свойства из схемы
    /// </summary>
    private IReadOnlyList<IrProperty> ExtractProperties(IOpenApiSchema schema, OpenApiDocument document, string? parentModelName = null)
    {
        var properties = new List<IrProperty>();
        
        if (schema.Properties == null)
            return properties;
        
        foreach (var (propertyName, propertySchema) in schema.Properties)
        {
            var sanitizedName = _namingConverter.SanitizeIdentifier(propertyName);
            var propertyType = BuildType(propertySchema, document, propertyContext: new PropertyContext
            {
                PropertyName = propertyName,
                ParentModelName = parentModelName
            });
            var isRequired = schema.Required?.Contains(propertyName) ?? false;
            var constraints = ExtractConstraints(propertySchema);
            
            properties.Add(new IrProperty
            {
                Name = sanitizedName,
                Type = propertyType,
                IsRequired = isRequired,
                Description = propertySchema.Description,
                Constraints = constraints
            });
        }
        
        return properties;
    }
    
    /// <summary>
    /// Контекст для генерации имен inline моделей
    /// </summary>
    private sealed class PropertyContext
    {
        public string? PropertyName { get; init; }
        public string? ParentModelName { get; init; }
        public bool IsForm { get; init; }
        public string? OperationId { get; init; }
    }
    
    /// <summary>
    /// Извлекает ограничения из схемы
    /// </summary>
    private IrPropertyConstraints? ExtractConstraints(IOpenApiSchema schema)
    {
        var hasConstraints = !string.IsNullOrEmpty(schema.Minimum) ||
                            !string.IsNullOrEmpty(schema.Maximum) ||
                            schema.MinLength.HasValue ||
                            schema.MaxLength.HasValue ||
                            !string.IsNullOrEmpty(schema.Pattern) ||
                            schema.Default != null;
        
        if (!hasConstraints)
            return null;
        
        decimal? min = null;
        decimal? max = null;
        if (decimal.TryParse(schema.Minimum, out var parsedMin))
            min = parsedMin;
        if (decimal.TryParse(schema.Maximum, out var parsedMax))
            max = parsedMax;
        
        decimal? exclusiveMin = null;
        decimal? exclusiveMax = null;
        if (decimal.TryParse(schema.ExclusiveMinimum, out var parsedExclusiveMin))
            exclusiveMin = parsedExclusiveMin;
        if (decimal.TryParse(schema.ExclusiveMaximum, out var parsedExclusiveMax))
            exclusiveMax = parsedExclusiveMax;
        
        return new IrPropertyConstraints
        {
            Min = min,
            Max = max,
            ExclusiveMin = exclusiveMin,
            ExclusiveMax = exclusiveMax,
            MinLength = schema.MinLength,
            MaxLength = schema.MaxLength,
            Pattern = schema.Pattern,
            DefaultValue = ExtractDefaultValue(schema.Default)
        };
    }
    
    /// <summary>
    /// Извлекает значение по умолчанию
    /// </summary>
    private object? ExtractDefaultValue(JsonNode? defaultValue)
    {
        if (defaultValue == null)
            return null;
        
        // Извлекаем значение из JsonNode
        return defaultValue switch
        {
            JsonValue jsonValue => jsonValue.GetValue<object>(),
            JsonObject jsonObject => jsonObject,
            JsonArray jsonArray => jsonArray,
            _ => defaultValue.ToString()
        };
    }
    
    /// <summary>
    /// Извлекает композицию из схемы
    /// </summary>
    private IrComposition? ExtractComposition(IOpenApiSchema schema, OpenApiDocument document, PropertyContext? propertyContext = null)
    {
        if (schema.AllOf?.Count > 0)
        {
            var types = schema.AllOf.Select(s => BuildType(s, document, propertyContext)).ToList();
            return new IrComposition
            {
                Kind = IrCompositionKind.AllOf,
                Types = types
            };
        }
        
        if (schema.OneOf?.Count > 0)
        {
            var types = schema.OneOf.Select(s => BuildType(s, document, propertyContext)).ToList();
            return new IrComposition
            {
                Kind = IrCompositionKind.OneOf,
                Types = types,
                DiscriminatorPropertyName = schema.Discriminator?.PropertyName,
                DiscriminatorMapping = schema.Discriminator?.Mapping?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Id ?? string.Empty
                )
            };
        }
        
        if (schema.AnyOf?.Count > 0)
        {
            var types = schema.AnyOf.Select(s => BuildType(s, document, propertyContext)).ToList();
            return new IrComposition
            {
                Kind = IrCompositionKind.AnyOf,
                Types = types
            };
        }
        
        return null;
    }
    
    /// <summary>
    /// Строит тип из схемы (рекурсивно)
    /// </summary>
    private IrType BuildType(IOpenApiSchema schema, OpenApiDocument document, PropertyContext? propertyContext = null)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        
        // СНАЧАЛА проверяем ссылки ДО разрешения (после разрешения информация о ссылке теряется)
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> referenceHolder)
        {
            var reference = referenceHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                var modelName = _namingConverter.SanitizeIdentifier(reference.Id);
                return new IrModelReference { ModelName = modelName };
            }
        }
        
        // Разрешаем ссылки
        var resolvedSchema = _schemaResolver.ResolveSchema(schema);
        
        if (resolvedSchema == null)
            throw new InvalidOperationException("Schema resolver returned null schema");
        
        // OneOf, AnyOf -> Union (проверяем до проверки Type, так как композиция может не иметь Type)
        if (resolvedSchema.OneOf?.Count > 0 || resolvedSchema.AnyOf?.Count > 0)
        {
            var variants = (resolvedSchema.OneOf ?? resolvedSchema.AnyOf)!;
            var types = variants.Select(v => BuildType(v, document, propertyContext)).ToList();
            return new IrUnionType { Types = types };
        }
        
        // AllOf -> Intersection (проверяем до проверки Type)
        if (resolvedSchema.AllOf?.Count > 0)
        {
            var types = resolvedSchema.AllOf.Select(s => BuildType(s, document, propertyContext)).ToList();
            return new IrIntersectionType { Types = types };
        }
        
        // Если Type не указан, но есть Properties - это объект
        if (!resolvedSchema.Type.HasValue)
        {
            // Если есть Properties, это объект с полями
            if (resolvedSchema.Properties != null && resolvedSchema.Properties.Count > 0)
            {
                // Проверяем, является ли это словарем (есть AdditionalProperties и это не false)
                // В OpenAPI AdditionalProperties может быть IOpenApiAny (boolean) или IOpenApiSchema
                if (resolvedSchema.AdditionalProperties != null)
                {
                    // Если это IOpenApiSchema, это словарь
                    if (resolvedSchema.AdditionalProperties is IOpenApiSchema schemaValue)
                    {
                        var keyType = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String };
                        var valueType = BuildType(schemaValue, document, propertyContext);
                        return new IrDictionaryType { KeyType = keyType, ValueType = valueType };
                    }
                    
                    // Если это не IOpenApiSchema, это может быть boolean false
                    // В этом случае это объект с фиксированными свойствами, не словарь
                    // Продолжаем обработку как inline объект ниже
                }
                
                // Это inline объект с фиксированными свойствами
                // Создаем inline модель с правильным именем на основе контекста
                var inlineModelName = GenerateInlineModelNameFromContext(propertyContext);
                var inlineModel = BuildModel(inlineModelName, resolvedSchema, document, propertyContext?.ParentModelName, propertyContext);
                _inlineModels[inlineModelName] = inlineModel;
                return new IrModelReference { ModelName = inlineModelName };
            }
            
            // Если есть AdditionalProperties, это словарь
            if (resolvedSchema.AdditionalProperties != null)
            {
                var keyType = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String };
                var valueType = BuildType(resolvedSchema.AdditionalProperties, document, propertyContext);
                return new IrDictionaryType { KeyType = keyType, ValueType = valueType };
            }
            
            // Если ничего нет, возвращаем any
            return new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Any };
        }
        
        // Примитивные типы
        if (IsPrimitive(resolvedSchema))
        {
            return new IrPrimitiveType
            {
                PrimitiveKind = MapPrimitive(resolvedSchema.Type, resolvedSchema.Format)
            };
        }
        
        // Массив
        if (resolvedSchema.Type.Value.HasFlag(JsonSchemaType.Array))
        {
            if (resolvedSchema.Items == null)
                throw new InvalidOperationException("Array schema must have Items property");
            
            var elementType = BuildType(resolvedSchema.Items, document, propertyContext);
            return new IrArrayType { ElementType = elementType };
        }
        
        // Object - проверяем, является ли это словарем или объектом с фиксированными свойствами
        if (resolvedSchema.Type.Value.HasFlag(JsonSchemaType.Object))
        {
            // Словарь: object с AdditionalProperties (не false) - это словарь
            // additionalProperties: false означает, что это НЕ словарь, а объект с фиксированными свойствами
            if (resolvedSchema.AdditionalProperties != null)
            {
                // Если это IOpenApiSchema, это словарь
                if (resolvedSchema.AdditionalProperties is IOpenApiSchema schemaValue)
                {
                    var keyType = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String };
                    var valueType = BuildType(schemaValue, document);
                    return new IrDictionaryType { KeyType = keyType, ValueType = valueType };
                }
                
                // Если это не IOpenApiSchema, это может быть boolean false
                // В этом случае это объект с фиксированными свойствами, не словарь
                // Продолжаем обработку ниже
            }
            
            // Если есть Properties, это объект с фиксированными полями
            // Для inline объектов (не в components/schemas) создаем отдельную модель
            if (resolvedSchema.Properties != null && resolvedSchema.Properties.Count > 0)
            {
                // Это inline объект - создаем для него модель с правильным именем на основе контекста
                var inlineModelName = GenerateInlineModelNameFromContext(propertyContext);
                var inlineModel = BuildModel(inlineModelName, resolvedSchema, document, propertyContext?.ParentModelName, propertyContext);
                _inlineModels[inlineModelName] = inlineModel;
                return new IrModelReference { ModelName = inlineModelName };
            }
            
            // Пустой объект без Properties и AdditionalProperties
            var emptyValueType = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Any };
            var emptyKeyType = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String };
            return new IrDictionaryType { KeyType = emptyKeyType, ValueType = emptyValueType };
        }
        
        var typeStr = resolvedSchema.Type?.ToString() ?? "null";
        throw new InvalidOperationException($"Unknown schema type: {typeStr}");
    }
    
    /// <summary>
    /// Проверяет, является ли схема примитивным типом
    /// </summary>
    private bool IsPrimitive(IOpenApiSchema schema)
    {
        if (!schema.Type.HasValue)
            return false;
        
        var type = schema.Type.Value;
        return type.HasFlag(JsonSchemaType.String) || 
               type.HasFlag(JsonSchemaType.Number) || 
               type.HasFlag(JsonSchemaType.Integer) || 
               type.HasFlag(JsonSchemaType.Boolean);
    }
    
    /// <summary>
    /// Маппит примитивный тип OpenAPI в IrPrimitiveKind
    /// </summary>
    private IrPrimitiveKind MapPrimitive(JsonSchemaType? type, string? format)
    {
        if (!type.HasValue)
            return IrPrimitiveKind.String;
        
        var typeValue = type.Value;
        
        if (typeValue.HasFlag(JsonSchemaType.String))
        {
            return format switch
            {
                "date" => IrPrimitiveKind.Date,
                "date-time" => IrPrimitiveKind.DateTime,
                "time" => IrPrimitiveKind.Time,
                "uuid" or "guid" => IrPrimitiveKind.Uuid,
                "binary" => IrPrimitiveKind.Binary,
                "byte" => IrPrimitiveKind.ByteArray,
                _ => IrPrimitiveKind.String
            };
        }
        
        if (typeValue.HasFlag(JsonSchemaType.Number))
            return IrPrimitiveKind.Number;
        
        if (typeValue.HasFlag(JsonSchemaType.Integer))
            return IrPrimitiveKind.Integer;
        
        if (typeValue.HasFlag(JsonSchemaType.Boolean))
            return IrPrimitiveKind.Boolean;
        
        return IrPrimitiveKind.String;
    }
    
    /// <summary>
    /// Извлекает enum'ы из компонентов
    /// </summary>
    private IReadOnlyList<IrEnum> ExtractEnums(OpenApiDocument document)
    {
        var enums = new List<IrEnum>();
        
        if (document.Components?.Schemas == null)
            return enums;
        
        foreach (var (schemaName, schema) in document.Components.Schemas)
        {
            // Enum должен иметь непустой список значений и тип string или integer
            if (schema.Enum == null || schema.Enum.Count == 0)
                continue;
            
            // Enum должен быть либо string, либо integer (без других типов)
            if (!schema.Type.HasValue)
                continue;
            
            var type = schema.Type.Value;
            var isStringEnum = type.HasFlag(JsonSchemaType.String) && !type.HasFlag(JsonSchemaType.Integer) && !type.HasFlag(JsonSchemaType.Number);
            var isIntegerEnum = type.HasFlag(JsonSchemaType.Integer) && !type.HasFlag(JsonSchemaType.String) && !type.HasFlag(JsonSchemaType.Number);
            
            if (!isStringEnum && !isIntegerEnum)
                continue;
            
            // Если есть Properties, это не enum, а модель с enum полем
            if (schema.Properties != null && schema.Properties.Count > 0)
                continue;
            
            var sanitizedName = _namingConverter.SanitizeIdentifier(schemaName);
            var enumModel = BuildEnum(sanitizedName, schema);
            enums.Add(enumModel);
        }
        
        return enums;
    }
    
    /// <summary>
    /// Строит enum из схемы
    /// </summary>
    private IrEnum BuildEnum(string name, IOpenApiSchema schema)
    {
        var values = new List<IrEnumValue>();
        var baseType = schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Integer) 
            ? IrEnumBaseType.Integer 
            : IrEnumBaseType.String;
        
        if (schema.Enum != null)
        {
            foreach (var enumValue in schema.Enum)
            {
                var valueName = _namingConverter.SanitizeIdentifier(enumValue.ToString() ?? "Unknown");
                values.Add(new IrEnumValue
                {
                    Name = valueName,
                    Value = enumValue
                });
            }
        }
        
        return new IrEnum
        {
            Name = name,
            Description = schema.Description,
            Values = values,
            BaseType = baseType,
            OriginalSchema = schema
        };
    }
    
    /// <summary>
    /// Извлекает клиенты из путей
    /// </summary>
    private IReadOnlyList<IrClient> ExtractClients(OpenApiDocument document)
    {
        var operations = new List<IrOperation>();
        
        if (document.Paths == null)
            return Array.Empty<IrClient>();
        
        foreach (var pathEntry in document.Paths)
        {
            var path = pathEntry.Key;
            var pathItem = pathEntry.Value;
            
            if (pathItem.Operations == null)
                continue;
            
            foreach (var operationEntry in pathItem.Operations)
            {
                var httpMethod = operationEntry.Key;
                var openApiOperation = operationEntry.Value;
                
                if (openApiOperation == null)
                    continue;
                
                var operation = BuildOperation(path, httpMethod, openApiOperation, document);
                operations.Add(operation);
            }
        }
        
        // Группируем операции по тегам (если есть) или создаем один клиент
        if (operations.Count == 0)
            return Array.Empty<IrClient>();
        
        var clients = new List<IrClient>();
        var operationsByTag = operations
            .Where(op => op.Tags != null && op.Tags.Count > 0)
            .GroupBy(op => op.Tags!.First())
            .ToList();
        
        // Клиенты по тегам
        foreach (var tagGroup in operationsByTag)
        {
            var clientName = _namingConverter.SanitizeIdentifier(tagGroup.Key);
            clients.Add(new IrClient
            {
                Name = clientName,
                Operations = tagGroup.ToList()
            });
        }
        
        // Операции без тегов или с пустыми тегами
        var operationsWithoutTags = operations
            .Where(op => op.Tags == null || op.Tags.Count == 0)
            .ToList();
        
        if (operationsWithoutTags.Count > 0)
        {
            clients.Add(new IrClient
            {
                Name = "ApiClient",
                Operations = operationsWithoutTags
            });
        }
        
        // Если все операции были сгруппированы по тегам, возвращаем клиенты по тегам
        // Иначе возвращаем один клиент со всеми операциями
        if (clients.Count == 0 && operations.Count > 0)
        {
            clients.Add(new IrClient
            {
                Name = "ApiClient",
                Operations = operations
            });
        }
        
        return clients;
    }
    
    /// <summary>
    /// Строит IrOperation из OpenAPI операции
    /// </summary>
    private IrOperation BuildOperation(
        string path,
        HttpMethod httpMethod,
        OpenApiOperation openApiOperation,
        OpenApiDocument document)
    {
        var operationId = openApiOperation.OperationId ?? 
            $"{httpMethod.Method.ToLower()}{path.Replace("/", "").Replace("{", "").Replace("}", "")}";
        var methodName = _namingConverter.SanitizeIdentifier(operationId);
        
        // Извлекаем параметры
        var pathParameters = new List<IrParameter>();
        var queryParameters = new List<IrParameter>();
        var headerParameters = new List<IrParameter>();
        
        if (openApiOperation.Parameters != null)
        {
            foreach (var parameter in openApiOperation.Parameters)
            {
                var irParameter = BuildParameter(parameter, document);
                
                switch (parameter.In)
                {
                    case ParameterLocation.Path:
                        pathParameters.Add(irParameter);
                        break;
                    case ParameterLocation.Query:
                        queryParameters.Add(irParameter);
                        break;
                    case ParameterLocation.Header:
                        headerParameters.Add(irParameter);
                        break;
                }
            }
        }
        
        // Извлекаем request body (передаем operationId для генерации FormModel)
        IrRequestBody? requestBody = null;
        if (openApiOperation.RequestBody != null)
        {
            requestBody = BuildRequestBody(openApiOperation.RequestBody, document, operationId);
        }
        
        // Извлекаем responses
        var responses = new List<IrResponse>();
        if (openApiOperation.Responses != null)
        {
            foreach (var responseEntry in openApiOperation.Responses)
            {
                var statusCode = responseEntry.Key;
                var response = responseEntry.Value;
                
                var responseType = ExtractResponseType(response, document);
                
                responses.Add(new IrResponse
                {
                    StatusCode = statusCode,
                    Type = responseType,
                    Description = response.Description
                });
            }
        }
        
        return new IrOperation
        {
            MethodName = methodName,
            HttpMethod = httpMethod.Method.ToUpperInvariant(),
            Path = path,
            Summary = openApiOperation.Summary,
            Description = openApiOperation.Description,
            PathParameters = pathParameters,
            QueryParameters = queryParameters,
            HeaderParameters = headerParameters,
            RequestBody = requestBody,
            Responses = responses,
            Tags = openApiOperation.Tags?.Select(t => t.Name).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList() ?? new List<string>()
        };
    }
    
    /// <summary>
    /// Строит IrParameter из OpenAPI параметра
    /// </summary>
    private IrParameter BuildParameter(IOpenApiParameter parameter, OpenApiDocument document)
    {
        var name = _namingConverter.SanitizeIdentifier(parameter.Name ?? "param");
        var defaultSchema = new OpenApiSchema { Type = JsonSchemaType.String };
        var type = BuildType(parameter.Schema ?? defaultSchema, document);
        var isRequired = parameter.Required;
        
        return new IrParameter
        {
            Name = name,
            Type = type,
            IsRequired = isRequired,
            Description = parameter.Description
        };
    }
    
    /// <summary>
    /// Строит IrRequestBody из OpenAPI request body
    /// </summary>
    private IrRequestBody BuildRequestBody(IOpenApiRequestBody requestBody, OpenApiDocument document, string? operationId = null)
    {
        // Берем первый content type (обычно application/json)
        var contentType = requestBody.Content?.Keys.FirstOrDefault();
        var content = contentType != null && requestBody.Content != null
            ? requestBody.Content[contentType]
            : null;
        
        var schema = content?.Schema;
        
        // Определяем, является ли это формой (multipart/form-data или application/x-www-form-urlencoded)
        var isForm = contentType == "multipart/form-data" || contentType == "application/x-www-form-urlencoded";
        
        var type = schema != null
            ? BuildType(schema, document, propertyContext: isForm && !string.IsNullOrEmpty(operationId)
                ? new PropertyContext
                {
                    IsForm = true,
                    OperationId = operationId
                }
                : null)
            : new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String };
        
        return new IrRequestBody
        {
            Type = type,
            IsRequired = requestBody.Required,
            ContentType = contentType,
            Description = requestBody.Description
        };
    }
    
    /// <summary>
    /// Извлекает тип ответа из OpenAPI response
    /// </summary>
    private IrType? ExtractResponseType(IOpenApiResponse response, OpenApiDocument document)
    {
        // Берем первый content type
        var content = response.Content?.FirstOrDefault();
        var schema = content.HasValue ? content.Value.Value?.Schema : null;
        
        if (schema == null)
            return null;
        
        return BuildType(schema, document);
    }
    
    /// <summary>
    /// Строит граф зависимостей типов
    /// </summary>
    private IrTypeDependencyGraph BuildTypeDependencyGraph(
        IReadOnlyList<IrModel> models,
        IReadOnlyList<IrEnum> enums,
        IReadOnlyList<IrClient> clients)
    {
        var dependencies = new Dictionary<string, HashSet<string>>();
        var allTypeNames = new HashSet<string>();
        
        // Добавляем все типы в граф
        foreach (var model in models)
        {
            allTypeNames.Add(model.Name);
            dependencies[model.Name] = new HashSet<string>();
        }
        
        foreach (var enumModel in enums)
        {
            allTypeNames.Add(enumModel.Name);
            dependencies[enumModel.Name] = new HashSet<string>();
        }
        
        // Собираем зависимости из моделей
        foreach (var model in models)
        {
            var modelDependencies = ExtractDependenciesFromModel(model);
            dependencies[model.Name] = modelDependencies;
        }
        
        // Собираем зависимости из операций клиентов
        foreach (var client in clients)
        {
            foreach (var operation in client.Operations)
            {
                // Зависимости из параметров
                foreach (var param in operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters))
                {
                    var paramDeps = ExtractDependenciesFromType(param.Type);
                    foreach (var dep in paramDeps)
                    {
                        if (allTypeNames.Contains(dep))
                        {
                            // Зависимость от другого типа в IR
                            // Можно добавить в зависимости клиента, но пока просто собираем
                        }
                    }
                }
                
                // Зависимости из request body
                if (operation.RequestBody != null)
                {
                    var bodyDeps = ExtractDependenciesFromType(operation.RequestBody.Type);
                    // Аналогично
                }
                
                // Зависимости из responses
                foreach (var response in operation.Responses)
                {
                    if (response.Type != null)
                    {
                        var responseDeps = ExtractDependenciesFromType(response.Type);
                        // Аналогично
                    }
                }
            }
        }
        
        // Топологическая сортировка
        var topologicalOrder = TopologicalSort(dependencies, allTypeNames);
        
        // Преобразуем в ImmutableDictionary
        var immutableDependencies = dependencies.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value.ToList()
        );
        
        return new IrTypeDependencyGraph
        {
            Dependencies = immutableDependencies.ToImmutableDictionary(),
            TopologicalOrder = topologicalOrder
        };
    }
    
    /// <summary>
    /// Извлекает зависимости из модели
    /// </summary>
    private HashSet<string> ExtractDependenciesFromModel(IrModel model)
    {
        var dependencies = new HashSet<string>();
        
        foreach (var property in model.Properties)
        {
            var deps = ExtractDependenciesFromType(property.Type);
            dependencies.UnionWith(deps);
        }
        
        if (model.Composition != null)
        {
            foreach (var type in model.Composition.Types)
            {
                var deps = ExtractDependenciesFromType(type);
                dependencies.UnionWith(deps);
            }
        }
        
        return dependencies;
    }
    
    /// <summary>
    /// Извлекает зависимости из типа
    /// </summary>
    private HashSet<string> ExtractDependenciesFromType(IrType type)
    {
        var dependencies = new HashSet<string>();
        
        switch (type)
        {
            case IrModelReference modelRef:
                dependencies.Add(modelRef.ModelName);
                break;
            case IrArrayType arrayType:
                dependencies.UnionWith(ExtractDependenciesFromType(arrayType.ElementType));
                break;
            case IrDictionaryType dictType:
                dependencies.UnionWith(ExtractDependenciesFromType(dictType.KeyType));
                dependencies.UnionWith(ExtractDependenciesFromType(dictType.ValueType));
                break;
            case IrUnionType unionType:
                foreach (var t in unionType.Types)
                    dependencies.UnionWith(ExtractDependenciesFromType(t));
                break;
            case IrIntersectionType intersectionType:
                foreach (var t in intersectionType.Types)
                    dependencies.UnionWith(ExtractDependenciesFromType(t));
                break;
            case IrNullableType nullableType:
                dependencies.UnionWith(ExtractDependenciesFromType(nullableType.InnerType));
                break;
            case IrOptionalType optionalType:
                dependencies.UnionWith(ExtractDependenciesFromType(optionalType.InnerType));
                break;
        }
        
        return dependencies;
    }
    
    /// <summary>
    /// Топологическая сортировка графа зависимостей
    /// </summary>
    private IReadOnlyList<string> TopologicalSort(
        Dictionary<string, HashSet<string>> dependencies,
        HashSet<string> allTypeNames)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        
        void Visit(string typeName)
        {
            if (visiting.Contains(typeName))
            {
                // Циклическая зависимость - просто добавляем в конец
                return;
            }
            
            if (visited.Contains(typeName))
                return;
            
            visiting.Add(typeName);
            
            if (dependencies.TryGetValue(typeName, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (allTypeNames.Contains(dep))
                    {
                        Visit(dep);
                    }
                }
            }
            
            visiting.Remove(typeName);
            visited.Add(typeName);
            result.Add(typeName);
        }
        
        foreach (var typeName in allTypeNames)
        {
            if (!visited.Contains(typeName))
            {
                Visit(typeName);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Генерирует имя для inline модели на основе контекста
    /// </summary>
    private string GenerateInlineModelNameFromContext(PropertyContext? propertyContext)
    {
        // Если это форма, генерируем {operationId}FormModel
        if (propertyContext?.IsForm == true && !string.IsNullOrEmpty(propertyContext.OperationId))
        {
            var operationIdPascal = _namingConverter.ToPascalCase(propertyContext.OperationId);
            return _namingConverter.SanitizeIdentifier($"{operationIdPascal}FormModel");
        }
        
        // Если есть имя свойства, генерируем {propertyName}InlineModel
        if (!string.IsNullOrEmpty(propertyContext?.PropertyName))
        {
            var propertyNamePascal = _namingConverter.ToPascalCase(propertyContext.PropertyName);
            return _namingConverter.SanitizeIdentifier($"{propertyNamePascal}InlineModel");
        }
        
        // Fallback: используем счетчик
        return _namingConverter.SanitizeIdentifier($"InlineModel{_inlineModelCounter++}");
    }
}
