using System.Collections.Immutable;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
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
        var models = ExtractModels(document);
        var enums = ExtractEnums(document);
        var clients = ExtractClients(document);
        var typeDependencies = BuildTypeDependencyGraph(models, enums, clients);
        
        return new IrDocument
        {
            Metadata = ExtractMetadata(document),
            Models = models,
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
            if (schema.Enum?.Count > 0 && schema.Type == "string")
                continue;
            
            var sanitizedName = _namingConverter.SanitizeIdentifier(schemaName);
            var model = BuildModel(sanitizedName, schema, document);
            models.Add(model);
        }
        
        return models;
    }
    
    /// <summary>
    /// Строит модель из схемы
    /// </summary>
    private IrModel BuildModel(string name, OpenApiSchema schema, OpenApiDocument document)
    {
        var properties = ExtractProperties(schema, document);
        var composition = ExtractComposition(schema, document);
        
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
    private IReadOnlyList<IrProperty> ExtractProperties(OpenApiSchema schema, OpenApiDocument document)
    {
        var properties = new List<IrProperty>();
        
        if (schema.Properties == null)
            return properties;
        
        foreach (var (propertyName, propertySchema) in schema.Properties)
        {
            var sanitizedName = _namingConverter.SanitizeIdentifier(propertyName);
            var propertyType = BuildType(propertySchema, document);
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
    /// Извлекает ограничения из схемы
    /// </summary>
    private IrPropertyConstraints? ExtractConstraints(OpenApiSchema schema)
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
    private object? ExtractDefaultValue(IOpenApiAny? defaultValue)
    {
        if (defaultValue == null)
            return null;
        
        // Простая реализация - можно улучшить
        return defaultValue.ToString();
    }
    
    /// <summary>
    /// Извлекает композицию из схемы
    /// </summary>
    private IrComposition? ExtractComposition(OpenApiSchema schema, OpenApiDocument document)
    {
        if (schema.AllOf?.Count > 0)
        {
            var types = schema.AllOf.Select(s => BuildType(s, document)).ToList();
            return new IrComposition
            {
                Kind = IrCompositionKind.AllOf,
                Types = types
            };
        }
        
        if (schema.OneOf?.Count > 0)
        {
            var types = schema.OneOf.Select(s => BuildType(s, document)).ToList();
            return new IrComposition
            {
                Kind = IrCompositionKind.OneOf,
                Types = types,
                DiscriminatorPropertyName = schema.Discriminator?.PropertyName,
                DiscriminatorMapping = schema.Discriminator?.Mapping?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                )
            };
        }
        
        if (schema.AnyOf?.Count > 0)
        {
            var types = schema.AnyOf.Select(s => BuildType(s, document)).ToList();
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
    private IrType BuildType(IOpenApiSchema schema, OpenApiDocument document)
    {
        // Разрешаем ссылки
        var resolvedSchema = _schemaResolver.ResolveSchema(schema, document);
        
        // Примитивные типы
        if (IsPrimitive(resolvedSchema))
        {
            return new IrPrimitiveType
            {
                PrimitiveKind = MapPrimitive(resolvedSchema.Type, resolvedSchema.Format)
            };
        }
        
        // Массив
        if (resolvedSchema.Type == "array" && resolvedSchema.Items != null)
        {
            var elementType = BuildType(resolvedSchema.Items, document);
            return new IrArrayType { ElementType = elementType };
        }
        
        // Словарь
        if (resolvedSchema.Type == "object" && resolvedSchema.AdditionalProperties != null)
        {
            var keyType = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String };
            var valueType = BuildType(resolvedSchema.AdditionalProperties, document);
            return new IrDictionaryType { KeyType = keyType, ValueType = valueType };
        }
        
        // OneOf, AnyOf -> Union
        if (resolvedSchema.OneOf?.Count > 0 || resolvedSchema.AnyOf?.Count > 0)
        {
            var variants = (resolvedSchema.OneOf ?? resolvedSchema.AnyOf)!;
            var types = variants.Select(v => BuildType(v, document)).ToList();
            return new IrUnionType { Types = types };
        }
        
        // AllOf -> Intersection
        if (resolvedSchema.AllOf?.Count > 0)
        {
            var types = resolvedSchema.AllOf.Select(s => BuildType(s, document)).ToList();
            return new IrIntersectionType { Types = types };
        }
        
        // Ссылка на модель
        if (resolvedSchema.Reference != null)
        {
            var modelName = _namingConverter.SanitizeIdentifier(resolvedSchema.Reference.Id);
            return new IrModelReference { ModelName = modelName };
        }
        
        // Object (inline) - создаем анонимную модель или возвращаем reference
        if (resolvedSchema.Type == "object")
        {
            // TODO: Обработка inline объектов
            throw new NotImplementedException("Inline objects not yet supported");
        }
        
        throw new InvalidOperationException($"Unknown schema type: {resolvedSchema.Type}");
    }
    
    /// <summary>
    /// Проверяет, является ли схема примитивным типом
    /// </summary>
    private bool IsPrimitive(IOpenApiSchema schema)
    {
        return schema.Type == "string" || 
               schema.Type == "number" || 
               schema.Type == "integer" || 
               schema.Type == "boolean";
    }
    
    /// <summary>
    /// Маппит примитивный тип OpenAPI в IrPrimitiveKind
    /// </summary>
    private IrPrimitiveKind MapPrimitive(string? type, string? format)
    {
        return (type, format) switch
        {
            ("string", "date") => IrPrimitiveKind.Date,
            ("string", "date-time") => IrPrimitiveKind.DateTime,
            ("string", "time") => IrPrimitiveKind.Time,
            ("string", "uuid") or ("string", "guid") => IrPrimitiveKind.Uuid,
            ("string", "binary") => IrPrimitiveKind.Binary,
            ("string", "byte") => IrPrimitiveKind.ByteArray,
            ("string", _) => IrPrimitiveKind.String,
            ("number", _) => IrPrimitiveKind.Number,
            ("integer", _) => IrPrimitiveKind.Integer,
            ("boolean", _) => IrPrimitiveKind.Boolean,
            _ => IrPrimitiveKind.String
        };
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
            if (schema.Enum?.Count == 0)
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
    private IrEnum BuildEnum(string name, OpenApiSchema schema)
    {
        var values = new List<IrEnumValue>();
        var baseType = schema.Type == "integer" ? IrEnumBaseType.Integer : IrEnumBaseType.String;
        
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
        // TODO: Реализовать извлечение операций из путей
        // Пока возвращаем пустой список
        return Array.Empty<IrClient>();
    }
    
    /// <summary>
    /// Строит граф зависимостей типов
    /// </summary>
    private IrTypeDependencyGraph BuildTypeDependencyGraph(
        IReadOnlyList<IrModel> models,
        IReadOnlyList<IrEnum> enums,
        IReadOnlyList<IrClient> clients)
    {
        // TODO: Реализовать построение графа зависимостей
        // Пока возвращаем пустой граф
        return new IrTypeDependencyGraph
        {
            Dependencies = ImmutableDictionary<string, IReadOnlyList<string>>.Empty,
            TopologicalOrder = Array.Empty<string>()
        };
    }
}
