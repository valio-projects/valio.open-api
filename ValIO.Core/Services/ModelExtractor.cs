using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;

namespace ValIO.Core.Services;

/// <summary>
/// Реализация извлечения моделей из OpenAPI документа
/// </summary>
public class ModelExtractor : IModelExtractor
{
    private readonly ISchemaResolver _schemaResolver;
    private readonly INamingConverter _namingConverter;

    public ModelExtractor(ISchemaResolver schemaResolver, INamingConverter namingConverter)
    {
        _schemaResolver = schemaResolver;
        _namingConverter = namingConverter;
    }

    public IReadOnlyList<ApiModel> ExtractModels(OpenApiDocument document)
    {
        var models = new List<ApiModel>();

        if (document.Components?.Schemas == null)
            return models;

        foreach (var schemaEntry in document.Components.Schemas)
        {
            var schemaName = schemaEntry.Key;
            var schema = schemaEntry.Value;

            var apiModel = ExtractModel(schema, schemaName);
            models.Add(apiModel);
        }

        return models;
    }

    public ApiModel ExtractModel(IOpenApiSchema schema, string modelName)
    {
        var resolvedSchema = _schemaResolver.ResolveSchema(schema);
        var sanitizedName = _namingConverter.SanitizeIdentifier(modelName);

        var apiModel = new ApiModel
        {
            Name = sanitizedName,
            Description = resolvedSchema.Description,
            OriginalSchema = resolvedSchema,
            Properties = new List<ApiProperty>(),
            RequiredProperties = new List<string>()
        };

        // Обрабатываем allOf - объединяем свойства из всех схем
        if (resolvedSchema.AllOf != null && resolvedSchema.AllOf.Count > 0)
        {
            var allProperties = new Dictionary<string, ApiProperty>();
            var allRequired = new HashSet<string>();
            string? baseClassName = null;

            foreach (var subSchema in resolvedSchema.AllOf)
            {
                var resolvedSubSchema = _schemaResolver.ResolveSchema(subSchema);
                
                // Проверяем, является ли под-схема ссылкой на другую модель (базовый класс)
                if (resolvedSubSchema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder)
                {
                    var reference = refHolder.Reference;
                    if (reference != null && !string.IsNullOrEmpty(reference.Id))
                    {
                        // Это базовый класс - сохраняем его имя
                        baseClassName = _namingConverter.ToPascalCase(reference.Id);
                        continue; // Свойства базового класса не добавляем сюда
                    }
                }

                // Объединяем свойства из этой схемы
                if (resolvedSubSchema.Properties != null)
                {
                    foreach (var propertyEntry in resolvedSubSchema.Properties)
                    {
                        var propertyName = propertyEntry.Key;
                        var propertySchema = propertyEntry.Value;
                        var apiProperty = ExtractProperty(propertyName, propertySchema, resolvedSubSchema);
                        allProperties[propertyName] = apiProperty; // Перезаписываем, если уже есть
                    }
                }

                // Объединяем required свойства
                if (resolvedSubSchema.Required != null)
                {
                    foreach (var requiredProp in resolvedSubSchema.Required)
                    {
                        allRequired.Add(requiredProp);
                    }
                }
            }

            // Добавляем свойства из самой схемы (если есть)
            if (resolvedSchema.Properties != null)
            {
                foreach (var propertyEntry in resolvedSchema.Properties)
                {
                    var propertyName = propertyEntry.Key;
                    var propertySchema = propertyEntry.Value;
                    var apiProperty = ExtractProperty(propertyName, propertySchema, resolvedSchema);
                    allProperties[propertyName] = apiProperty;
                }
            }

            // Добавляем required из самой схемы
            if (resolvedSchema.Required != null)
            {
                foreach (var requiredProp in resolvedSchema.Required)
                {
                    allRequired.Add(requiredProp);
                }
            }

            apiModel.Properties = allProperties.Values.ToList();
            apiModel.RequiredProperties = allRequired.ToList();
            apiModel.BaseClassName = baseClassName;
        }
        else
        {
            // Обычная схема без allOf
            if (resolvedSchema.Properties != null)
            {
                var properties = new List<ApiProperty>();
                foreach (var propertyEntry in resolvedSchema.Properties)
                {
                    var propertyName = propertyEntry.Key;
                    var propertySchema = propertyEntry.Value;

                    var apiProperty = ExtractProperty(propertyName, propertySchema, resolvedSchema);
                    properties.Add(apiProperty);
                }

                apiModel.Properties = properties;
            }

            apiModel.RequiredProperties = resolvedSchema.Required?.ToList() ?? new List<string>();
        }

        return apiModel;
    }

    private ApiProperty ExtractProperty(string propertyName, IOpenApiSchema propertySchema, IOpenApiSchema parentSchema)
    {
        var resolvedPropertySchema = _schemaResolver.ResolveSchema(propertySchema);
        var isRequired = parentSchema.Required?.Contains(propertyName) ?? false;
        var isNullable = IsSchemaNullable(resolvedPropertySchema);

        // Извлекаем min/max значения
        // Minimum и Maximum в OpenAPI - это строки, нужно парсить их
        decimal? min = null;
        decimal? max = null;
        if (!string.IsNullOrEmpty(resolvedPropertySchema.Minimum))
        {
            if (decimal.TryParse(resolvedPropertySchema.Minimum, out var parsedMin))
            {
                min = parsedMin;
            }
        }
        if (!string.IsNullOrEmpty(resolvedPropertySchema.Maximum))
        {
            if (decimal.TryParse(resolvedPropertySchema.Maximum, out var parsedMax))
            {
                max = parsedMax;
            }
        }

        // Извлекаем exclusiveMin/exclusiveMax значения
        decimal? exclusiveMin = null;
        decimal? exclusiveMax = null;
        if (!string.IsNullOrEmpty(resolvedPropertySchema.ExclusiveMinimum))
        {
            if (decimal.TryParse(resolvedPropertySchema.ExclusiveMinimum, out var parsedExclusiveMin))
            {
                exclusiveMin = parsedExclusiveMin;
            }
        }
        if (!string.IsNullOrEmpty(resolvedPropertySchema.ExclusiveMaximum))
        {
            if (decimal.TryParse(resolvedPropertySchema.ExclusiveMaximum, out var parsedExclusiveMax))
            {
                exclusiveMax = parsedExclusiveMax;
            }
        }

        // Извлекаем minLength/maxLength для строк
        int? minLength = resolvedPropertySchema.MinLength;
        int? maxLength = resolvedPropertySchema.MaxLength;
        
        // Извлекаем pattern (regex) для строк
        string? pattern = resolvedPropertySchema.Pattern;

        // Извлекаем значение по умолчанию из JsonNode
        object? defaultValue = null;
        if (resolvedPropertySchema.Default != null)
        {
            defaultValue = ExtractDefaultValue(resolvedPropertySchema.Default);
        }

        return new ApiProperty
        {
            Name = propertyName,
            Type = "object", // Будет определено TypeMapper'ом
            IsRequired = isRequired,
            IsNullable = isNullable,
            Description = resolvedPropertySchema.Description,
            DefaultValue = defaultValue,
            Min = min,
            Max = max,
            ExclusiveMin = exclusiveMin,
            ExclusiveMax = exclusiveMax,
            MinLength = minLength,
            MaxLength = maxLength,
            Pattern = pattern
        };
    }

    /// <summary>
    /// Извлекает значение по умолчанию из JsonNode
    /// </summary>
    private static object? ExtractDefaultValue(JsonNode? jsonNode)
    {
        if (jsonNode == null)
            return null;

        return jsonNode switch
        {
            JsonValue jsonValue => jsonValue.GetValue<object>(),
            JsonObject jsonObject => jsonObject,
            JsonArray jsonArray => jsonArray,
            _ => jsonNode.ToString()
        };
    }

    private static bool IsSchemaNullable(IOpenApiSchema schema)
    {
        // Проверяем, содержит ли тип Null
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Null))
        {
            return true;
        }

        // Проверяем расширения для обратной совместимости с OpenAPI 3.0
        if (schema.Extensions != null && schema.Extensions.TryGetValue(OpenApiConstants.NullableExtension, out var nullableExtension))
        {
            if (nullableExtension is JsonNodeExtension { Node: { } jsonNode } && jsonNode.GetValueKind() == JsonValueKind.True)
            {
                return true;
            }
        }

        return false;
    }
}

