using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

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

    public IReadOnlyList<ModelTemplateData> ExtractModelTemplateData(OpenApiDocument document, GenerationLanguage language, ITypeMapper typeMapper)
    {
        var models = new List<ModelTemplateData>();

        if (document.Components?.Schemas == null)
            return models;

        var typeInfoBuilder = new TypeInfoBuilder(typeMapper, _schemaResolver, language);
        var dependencyCollector = new DependencyCollector(new DependencyAnalyzer(_namingConverter));

        foreach (var schemaEntry in document.Components.Schemas)
        {
            var schemaName = schemaEntry.Key;
            var schema = schemaEntry.Value;

            var modelTemplateData = ExtractModelTemplateData(schema, schemaName, language, typeMapper, typeInfoBuilder, dependencyCollector);
            models.Add(modelTemplateData);
        }

        return models;
    }

    public ModelTemplateData ExtractModelTemplateData(IOpenApiSchema schema, string modelName, GenerationLanguage language, ITypeMapper typeMapper)
    {
        return ExtractModelTemplateData(schema, modelName, language, typeMapper, null, null);
    }

    private ModelTemplateData ExtractModelTemplateData(IOpenApiSchema schema, string modelName, GenerationLanguage language, ITypeMapper typeMapper, TypeInfoBuilder? typeInfoBuilder, DependencyCollector? dependencyCollector)
    {
        typeInfoBuilder ??= new TypeInfoBuilder(typeMapper, _schemaResolver, language);
        dependencyCollector ??= new DependencyCollector(new DependencyAnalyzer(_namingConverter));

        var resolvedSchema = _schemaResolver.ResolveSchema(schema);
        var sanitizedName = _namingConverter.SanitizeIdentifier(modelName);

        var modelTemplateData = new ModelTemplateData
        {
            Name = sanitizedName,
            Description = resolvedSchema.Description,
            Language = language,
            Format = language == GenerationLanguage.CSharp ? ModelFormat.Class : ModelFormat.Interface,
            Properties = new List<PropertyTemplateData>(),
            BaseClassProperties = new List<PropertyTemplateData>(),
            OwnProperties = new List<PropertyTemplateData>()
        };

        // Обрабатываем allOf - объединяем свойства из всех схем
        if (resolvedSchema.AllOf != null && resolvedSchema.AllOf.Count > 0)
        {
            var allProperties = new Dictionary<string, PropertyTemplateData>();
            var baseClassProperties = new List<PropertyTemplateData>();
            var ownProperties = new List<PropertyTemplateData>();
            var allRequired = new HashSet<string>();
            string? baseClassName = null;

            foreach (var subSchema in resolvedSchema.AllOf)
            {
                // Проверяем, является ли под-схема ссылкой на другую модель (базовый класс)
                string? referencedSchemaName = null;
                bool isBaseClass = false;
                if (subSchema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder)
                {
                    var reference = refHolder.Reference;
                    if (reference != null && !string.IsNullOrEmpty(reference.Id))
                    {
                        // Это базовый класс - сохраняем его имя
                        referencedSchemaName = reference.Id;
                        baseClassName = _namingConverter.ToPascalCase(reference.Id);
                        isBaseClass = true;
                    }
                }
                
                // Разрешаем схему для извлечения свойств
                var resolvedSubSchema = _schemaResolver.ResolveSchema(subSchema);

                // Объединяем свойства из этой схемы
                if (resolvedSubSchema.Properties != null)
                {
                    foreach (var propertyEntry in resolvedSubSchema.Properties)
                    {
                        var propertyName = propertyEntry.Key;
                        var propertySchema = propertyEntry.Value;
                        var propertyTemplateData = ExtractPropertyTemplateData(propertyName, propertySchema, resolvedSubSchema, typeInfoBuilder);
                        allProperties[propertyName] = propertyTemplateData;
                        
                        // Если это базовый класс, добавляем в baseClassProperties
                        if (isBaseClass)
                        {
                            baseClassProperties.Add(propertyTemplateData);
                        }
                        else
                        {
                            ownProperties.Add(propertyTemplateData);
                        }
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
                    var propertyTemplateData = ExtractPropertyTemplateData(propertyName, propertySchema, resolvedSchema, typeInfoBuilder);
                    allProperties[propertyName] = propertyTemplateData;
                    ownProperties.Add(propertyTemplateData);
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

            // Обновляем IsRequired для всех свойств
            foreach (var property in allProperties.Values)
            {
                property.IsRequired = allRequired.Contains(property.Name);
            }

            modelTemplateData.Properties = allProperties.Values.ToList();
            modelTemplateData.BaseClassProperties = baseClassProperties;
            modelTemplateData.OwnProperties = ownProperties;
            modelTemplateData.BaseClassName = baseClassName;
        }
        else
        {
            // Обычная схема без allOf
            if (resolvedSchema.Properties != null)
            {
                var properties = new List<PropertyTemplateData>();
                var required = resolvedSchema.Required?.ToHashSet() ?? new HashSet<string>();
                
                foreach (var propertyEntry in resolvedSchema.Properties)
                {
                    var propertyName = propertyEntry.Key;
                    var propertySchema = propertyEntry.Value;
                    var propertyTemplateData = ExtractPropertyTemplateData(propertyName, propertySchema, resolvedSchema, typeInfoBuilder);
                    propertyTemplateData.IsRequired = required.Contains(propertyName);
                    properties.Add(propertyTemplateData);
                }

                modelTemplateData.Properties = properties;
                modelTemplateData.OwnProperties = properties;
            }
        }

        // Собираем зависимости из всех свойств
        var allDependencies = new HashSet<string>();
        foreach (var property in modelTemplateData.Properties)
        {
            var propertyDependencies = dependencyCollector.CollectFromTypeInfo(property.TypeInfo);
            foreach (var dep in propertyDependencies)
            {
                allDependencies.Add(dep);
            }
        }
        modelTemplateData.Dependencies = allDependencies.ToList();

        return modelTemplateData;
    }

    private PropertyTemplateData ExtractPropertyTemplateData(string propertyName, IOpenApiSchema propertySchema, IOpenApiSchema parentSchema, TypeInfoBuilder typeInfoBuilder)
    {
        var resolvedPropertySchema = _schemaResolver.ResolveSchema(propertySchema);
        var isRequired = parentSchema.Required?.Contains(propertyName) ?? false;
        var isNullable = IsSchemaNullable(resolvedPropertySchema);

        // Создаем TypeInfo для свойства
        var typeInfo = typeInfoBuilder.BuildFromSchema(propertySchema, isNullable);
        
        // Определяем язык из TypeInfo
        var language = typeInfo.Language ?? GenerationLanguage.CSharp;

        // Извлекаем ограничения
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

        int? minLength = resolvedPropertySchema.MinLength;
        int? maxLength = resolvedPropertySchema.MaxLength;
        string? pattern = resolvedPropertySchema.Pattern;
        object? defaultValue = ExtractDefaultValue(resolvedPropertySchema.Default);

        // Определяем IsOptional для TypeScript
        var isOptional = !isRequired && (language == GenerationLanguage.TypeScript);

        return new PropertyTemplateData
        {
            Name = propertyName,
            TypeInfo = typeInfo,
            IsRequired = isRequired,
            IsNullable = isNullable,
            IsOptional = isOptional,
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
}

