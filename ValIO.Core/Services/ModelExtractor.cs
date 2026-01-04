using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;

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

    public IReadOnlyList<ModelTemplateData> ExtractModelTemplateData(OpenApiDocument document, ITypeMapper typeMapper, ITypeInfoBuilder typeInfoBuilder, IDependencyAnalyzer dependencyAnalyzer)
    {
        var models = new List<ModelTemplateData>(16);
        var inlineModels = new Dictionary<string, ModelTemplateData>(16);

        if (document.Components?.Schemas == null)
            return models;

        var dependencyCollector = new DependencyCollector(dependencyAnalyzer);

        // Первый проход: извлекаем основные модели и собираем информацию о вложенных схемах
        foreach (var schemaEntry in document.Components.Schemas)
        {
            var schemaName = schemaEntry.Key;
            var schema = schemaEntry.Value;

            var modelTemplateData = ExtractModelTemplateData(schema, schemaName, typeMapper, typeInfoBuilder, dependencyCollector, inlineModels, dependencyAnalyzer);
            models.Add(modelTemplateData);
        }

        // Второй проход: добавляем inline модели, которые были обнаружены
        foreach (var inlineModel in inlineModels.Values)
        {
            models.Add(inlineModel);
        }

        return models;
    }

    public ModelTemplateData ExtractModelTemplateData(IOpenApiSchema schema, string modelName, ITypeMapper typeMapper, ITypeInfoBuilder typeInfoBuilder, IDependencyAnalyzer dependencyAnalyzer)
    {
        var dependencyCollector = new DependencyCollector(dependencyAnalyzer);
        var inlineModels = new Dictionary<string, ModelTemplateData>(16);
        return ExtractModelTemplateData(schema, modelName, typeMapper, typeInfoBuilder, dependencyCollector, inlineModels, dependencyAnalyzer);
    }

    private ModelTemplateData ExtractModelTemplateData(IOpenApiSchema schema, string modelName, ITypeMapper typeMapper, ITypeInfoBuilder typeInfoBuilder, DependencyCollector dependencyCollector, Dictionary<string, ModelTemplateData> inlineModels, IDependencyAnalyzer dependencyAnalyzer)
    {

        var resolvedSchema = _schemaResolver.ResolveSchema(schema);
        var sanitizedName = _namingConverter.SanitizeIdentifier(modelName);

        // Определяем язык из TypeInfoBuilder
        var language = typeInfoBuilder.Language;

        var modelTemplateData = new ModelTemplateData
        {
            Name = sanitizedName,
            Description = resolvedSchema.Description,
            Language = language,
            Format = language == GenerationLanguage.CSharp ? ModelFormat.Class : ModelFormat.Interface,
            Properties = new List<PropertyTemplateData>(16),
            BaseClassProperties = new List<PropertyTemplateData>(8),
            OwnProperties = new List<PropertyTemplateData>(8),
            OriginalSchema = schema // Сохраняем оригинальную схему
        };
        
        // Создаем информацию о композиции схем (allOf, oneOf, anyOf)
        modelTemplateData.CompositionInfo = SchemaCompositionHelper.CreateCompositionInfo(
            schema, 
            resolvedSchema, 
            _schemaResolver, 
            _namingConverter);

        // Обрабатываем allOf - объединяем свойства из всех схем
        if (resolvedSchema.AllOf != null && resolvedSchema.AllOf.Count > 0)
        {
            var allProperties = new Dictionary<string, PropertyTemplateData>(32);
            var baseClassProperties = new List<PropertyTemplateData>(32);
            var ownProperties = new List<PropertyTemplateData>(32);
            var allRequired = new HashSet<string>(32);
            string? baseClassName = null;

            foreach (var subSchema in resolvedSchema.AllOf)
            {
                // Проверяем, является ли под-схема ссылкой на другую модель (базовый класс)
                // Используем унифицированный метод из SchemaCompositionHelper
                var referencedSchemaName = SchemaCompositionHelper.GetSchemaNameRaw(subSchema, _schemaResolver);
                bool isBaseClass = !string.IsNullOrEmpty(referencedSchemaName);
                bool isInlineSchema = false;
                string? inlineSchemaModelName = null;
                
                if (isBaseClass && !string.IsNullOrEmpty(referencedSchemaName))
                {
                    // Это базовый класс - сохраняем его имя
                    baseClassName = _namingConverter.ToPascalCase(referencedSchemaName);
                }
                
                // Разрешаем схему для извлечения свойств
                var resolvedSubSchema = _schemaResolver.ResolveSchema(subSchema);
                
                // Проверяем, является ли это inline схемой (без $ref, но с Properties)
                if (!isBaseClass && IsInlineObjectSchema(subSchema, resolvedSubSchema))
                {
                    // Создаем имя для inline модели в allOf
                    inlineSchemaModelName = $"{_namingConverter.ToPascalCase(modelName)}Inline";
                    isInlineSchema = true;
                    
                    // Создаем отдельную модель для inline схемы в allOf
                    if (inlineModels != null && !inlineModels.ContainsKey(inlineSchemaModelName))
                    {
                        var inlineModel = CreateInlineModel(resolvedSubSchema, inlineSchemaModelName, typeInfoBuilder, dependencyAnalyzer);
                        inlineModels[inlineSchemaModelName] = inlineModel;
                    }
                }

                // Если это inline схема в allOf, используем созданную модель как базовый класс
                if (isInlineSchema && !string.IsNullOrEmpty(inlineSchemaModelName))
                {
                    // Для inline схемы в allOf создаем intersection type (TypeScript) или наследование (C#)
                    // Используем имя созданной модели как базовый класс
                    if (string.IsNullOrEmpty(baseClassName))
                    {
                        baseClassName = _namingConverter.ToPascalCase(inlineSchemaModelName);
                    }
                    
                    // Извлекаем свойства из inline схемы
                    if (resolvedSubSchema.Properties != null)
                    {
                        foreach (var propertyEntry in resolvedSubSchema.Properties)
                        {
                            var propertyName = propertyEntry.Key;
                            var propertySchema = propertyEntry.Value;
                            var propertyTemplateData = ExtractPropertyTemplateData(propertyName, propertySchema, resolvedSubSchema, typeInfoBuilder, modelName, inlineModels, dependencyAnalyzer);
                            allProperties[propertyName] = propertyTemplateData;
                            ownProperties.Add(propertyTemplateData);
                        }
                    }
                }
                else if (resolvedSubSchema.Properties != null)
                {
                    // Объединяем свойства из этой схемы
                    foreach (var propertyEntry in resolvedSubSchema.Properties)
                    {
                        var propertyName = propertyEntry.Key;
                        var propertySchema = propertyEntry.Value;
                        var propertyTemplateData = ExtractPropertyTemplateData(propertyName, propertySchema, resolvedSubSchema, typeInfoBuilder, modelName, inlineModels, dependencyAnalyzer);
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
                    var propertyTemplateData = ExtractPropertyTemplateData(propertyName, propertySchema, resolvedSchema, typeInfoBuilder, modelName, inlineModels, dependencyAnalyzer);
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
                var properties = new List<PropertyTemplateData>(32);
                var required = resolvedSchema.Required?.ToHashSet() ?? new HashSet<string>();
                
                foreach (var propertyEntry in resolvedSchema.Properties)
                {
                    var propertyName = propertyEntry.Key;
                    var propertySchema = propertyEntry.Value;
                    var propertyTemplateData = ExtractPropertyTemplateData(propertyName, propertySchema, resolvedSchema, typeInfoBuilder, modelName, inlineModels, dependencyAnalyzer);
                    propertyTemplateData.IsRequired = required.Contains(propertyName);
                    properties.Add(propertyTemplateData);
                }

                modelTemplateData.Properties = properties;
                modelTemplateData.OwnProperties = properties;
            }
        }

        // Собираем зависимости из всех свойств
        var allDependencies = new HashSet<string>(32);
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

    private PropertyTemplateData ExtractPropertyTemplateData(string propertyName, IOpenApiSchema propertySchema, IOpenApiSchema parentSchema, ITypeInfoBuilder typeInfoBuilder, string? parentModelName = null, Dictionary<string, ModelTemplateData>? inlineModels = null, IDependencyAnalyzer? dependencyAnalyzer = null)
    {
        var resolvedPropertySchema = _schemaResolver.ResolveSchema(propertySchema);
        var isRequired = parentSchema.Required?.Contains(propertyName) ?? false;
        var isNullable = IsSchemaNullable(resolvedPropertySchema);
        
        // Сначала проверяем, является ли это ссылкой на компонент схемы
        // Если это ссылка, НЕ создаем inline модель - используем ссылку
        bool isComponentReference = false;
        if (propertySchema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder)
        {
            var reference = refHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id) && reference.Type == ReferenceType.Schema)
            {
                isComponentReference = true; // Это ссылка на компонент, не создаем inline модель
            }
        }
        
        // Проверяем, является ли схема свойства inline объектной схемой (без $ref, но с Properties)
        // Inline схема - это схема, которая определена прямо в свойстве, а не через ссылку на компонент
        if (!isComponentReference && inlineModels != null && IsInlineObjectSchema(propertySchema, resolvedPropertySchema))
        {
            // Создаем имя для inline модели на основе родительской модели и имени свойства
            var inlineModelName = GenerateInlineModelName(parentModelName, propertyName);
            
            // Проверяем, не создали ли мы уже такую модель
            if (!inlineModels.ContainsKey(inlineModelName))
            {
                // Создаем отдельную модель для inline схемы
                var inlineModel = CreateInlineModel(resolvedPropertySchema, inlineModelName, typeInfoBuilder, dependencyAnalyzer);
                inlineModels[inlineModelName] = inlineModel;
            }
            
            // Обновляем TypeInfo свойства, чтобы оно ссылалось на созданную модель
            var inlineModelNamePascal = _namingConverter.ToPascalCase(inlineModelName);
            var inlineTypeInfo = TypeInfo.Simple(inlineModelNamePascal, isPrimitive: false, language: typeInfoBuilder.Language);
            if (isNullable)
            {
                inlineTypeInfo = TypeInfo.Nullable(inlineTypeInfo);
            }
            
            // Создаем PropertyTemplateData с обновленным TypeInfo
            var inlinePropertyData = CreatePropertyTemplateData(propertyName, inlineTypeInfo, resolvedPropertySchema, isRequired, isNullable, typeInfoBuilder.Language);
            inlinePropertyData.OriginalSchema = propertySchema; // Сохраняем оригинальную схему
            return inlinePropertyData;
        }

        // Создаем TypeInfo для свойства (для ссылок или примитивов)
        var propertyTypeInfo = typeInfoBuilder.BuildFromSchema(propertySchema, isNullable);
        
        // Определяем язык из TypeInfoBuilder
        var builderLanguage = typeInfoBuilder.Language;

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
        var isOptional = !isRequired && (builderLanguage == GenerationLanguage.TypeScript);

        var propertyData = CreatePropertyTemplateData(propertyName, propertyTypeInfo, resolvedPropertySchema, isRequired, isNullable, builderLanguage, defaultValue, min, max, exclusiveMin, exclusiveMax, minLength, maxLength, pattern);
        propertyData.OriginalSchema = propertySchema; // Сохраняем оригинальную схему
        return propertyData;
    }
    
    /// <summary>
    /// Создает PropertyTemplateData с заданными параметрами
    /// </summary>
    private PropertyTemplateData CreatePropertyTemplateData(
        string propertyName,
        TypeInfo typeInfo,
        IOpenApiSchema resolvedPropertySchema,
        bool isRequired,
        bool isNullable,
        GenerationLanguage language,
        object? defaultValue = null,
        decimal? min = null,
        decimal? max = null,
        decimal? exclusiveMin = null,
        decimal? exclusiveMax = null,
        int? minLength = null,
        int? maxLength = null,
        string? pattern = null)
    {
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
    
    /// <summary>
    /// Проверяет, является ли схема inline объектной схемой (без $ref, но с Properties)
    /// Inline схема - это схема, которая определена прямо в свойстве, а не через ссылку на компонент
    /// </summary>
    private bool IsInlineObjectSchema(IOpenApiSchema originalSchema, IOpenApiSchema resolvedSchema)
    {
        // Проверяем, что это не ссылка на компонент схемы
        if (originalSchema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder)
        {
            var reference = refHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                // Проверяем, что это ссылка на компонент схемы (начинается с #/components/schemas/)
                if (reference.Type == ReferenceType.Schema)
                {
                    return false; // Это ссылка на компонент, не inline схема
                }
            }
        }
        
        // Проверяем, что это объект с Properties и нет ссылки на компонент
        if (resolvedSchema.Type.HasValue && resolvedSchema.Type.Value.HasFlag(JsonSchemaType.Object))
        {
            if (resolvedSchema.Properties != null && resolvedSchema.Properties.Count > 0)
            {
                // Дополнительная проверка: если resolvedSchema имеет Reference, это не inline
                if (resolvedSchema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> resolvedRefHolder)
                {
                    var resolvedRef = resolvedRefHolder.Reference;
                    if (resolvedRef != null && !string.IsNullOrEmpty(resolvedRef.Id) && resolvedRef.Type == ReferenceType.Schema)
                    {
                        return false; // Это ссылка на компонент
                    }
                }
                
                return true; // Это inline объектная схема
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Генерирует имя для inline модели на основе родительской модели и имени свойства
    /// </summary>
    private string GenerateInlineModelName(string? parentModelName, string propertyName)
    {
        var propertyNamePascal = _namingConverter.ToPascalCase(propertyName);
        
        if (!string.IsNullOrEmpty(parentModelName))
        {
            var parentNamePascal = _namingConverter.ToPascalCase(parentModelName);
            return $"{parentNamePascal}{propertyNamePascal}";
        }
        
        return propertyNamePascal;
    }
    
    /// <summary>
    /// Создает модель для inline схемы
    /// </summary>
    private ModelTemplateData CreateInlineModel(IOpenApiSchema schema, string modelName, ITypeInfoBuilder typeInfoBuilder, IDependencyAnalyzer dependencyAnalyzer)
    {
        var resolvedSchema = _schemaResolver.ResolveSchema(schema);
        var sanitizedName = _namingConverter.SanitizeIdentifier(modelName);
        var language = typeInfoBuilder.Language;
        var dependencyCollector = new DependencyCollector(dependencyAnalyzer);
        
        var model = new ModelTemplateData
        {
            Name = sanitizedName,
            Description = resolvedSchema.Description,
            Language = language,
            Format = language == GenerationLanguage.CSharp ? ModelFormat.Class : ModelFormat.Interface,
            Properties = new List<PropertyTemplateData>(16),
            BaseClassProperties = new List<PropertyTemplateData>(8),
            OwnProperties = new List<PropertyTemplateData>(8)
        };
        
        // Обрабатываем allOf для inline схемы
        if (resolvedSchema.AllOf != null && resolvedSchema.AllOf.Count > 0)
        {
            var allProperties = new Dictionary<string, PropertyTemplateData>(32);
            var baseClassProperties = new List<PropertyTemplateData>(32);
            var ownProperties = new List<PropertyTemplateData>(32);
            var allRequired = new HashSet<string>(32);
            string? baseClassName = null;
            
            foreach (var subSchema in resolvedSchema.AllOf)
            {
                // Проверяем, является ли под-схема ссылкой
                // Используем унифицированный метод из SchemaCompositionHelper
                var referencedSchemaName = SchemaCompositionHelper.GetSchemaNameRaw(subSchema, _schemaResolver);
                bool isBaseClass = !string.IsNullOrEmpty(referencedSchemaName);
                
                if (isBaseClass && !string.IsNullOrEmpty(referencedSchemaName))
                {
                    baseClassName = _namingConverter.ToPascalCase(referencedSchemaName);
                }
                
                var resolvedSubSchema = _schemaResolver.ResolveSchema(subSchema);
                
                if (resolvedSubSchema.Properties != null)
                {
                    foreach (var propertyEntry in resolvedSubSchema.Properties)
                    {
                        var propertyName = propertyEntry.Key;
                        var propertySchema = propertyEntry.Value;
                        // Не передаем inlineModels, чтобы избежать рекурсии для вложенных inline схем
                        var propertyTemplateData = ExtractPropertyTemplateData(propertyName, propertySchema, resolvedSubSchema, typeInfoBuilder, null, null);
                        allProperties[propertyName] = propertyTemplateData;
                        
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
                
                if (resolvedSubSchema.Required != null)
                {
                    foreach (var requiredProp in resolvedSubSchema.Required)
                    {
                        allRequired.Add(requiredProp);
                    }
                }
            }
            
            if (resolvedSchema.Properties != null)
            {
                foreach (var propertyEntry in resolvedSchema.Properties)
                {
                    var propertyName = propertyEntry.Key;
                    var propertySchema = propertyEntry.Value;
                    var propertyTemplateData = ExtractPropertyTemplateData(propertyName, propertySchema, resolvedSchema, typeInfoBuilder, null, null);
                    allProperties[propertyName] = propertyTemplateData;
                    ownProperties.Add(propertyTemplateData);
                }
            }
            
            if (resolvedSchema.Required != null)
            {
                foreach (var requiredProp in resolvedSchema.Required)
                {
                    allRequired.Add(requiredProp);
                }
            }
            
            foreach (var property in allProperties.Values)
            {
                property.IsRequired = allRequired.Contains(property.Name);
            }
            
            model.Properties = allProperties.Values.ToList();
            model.BaseClassProperties = baseClassProperties;
            model.OwnProperties = ownProperties;
            model.BaseClassName = baseClassName;
        }
        else
        {
            // Обычная схема без allOf
            if (resolvedSchema.Properties != null)
            {
                var properties = new List<PropertyTemplateData>(32);
                var required = resolvedSchema.Required?.ToHashSet() ?? new HashSet<string>();
                
                foreach (var propertyEntry in resolvedSchema.Properties)
                {
                    var propertyName = propertyEntry.Key;
                    var propertySchema = propertyEntry.Value;
                    // Не передаем inlineModels, чтобы избежать рекурсии
                    var propertyTemplateData = ExtractPropertyTemplateData(propertyName, propertySchema, resolvedSchema, typeInfoBuilder, null, null);
                    propertyTemplateData.IsRequired = required.Contains(propertyName);
                    properties.Add(propertyTemplateData);
                }
                
                model.Properties = properties;
                model.OwnProperties = properties;
            }
        }
        
        // Собираем зависимости
        var allDependencies = new HashSet<string>(32);
        foreach (var property in model.Properties)
        {
            var propertyDependencies = dependencyCollector.CollectFromTypeInfo(property.TypeInfo);
            foreach (var dep in propertyDependencies)
            {
                allDependencies.Add(dep);
            }
        }
        model.Dependencies = allDependencies.ToList();
        
        return model;
    }
}

