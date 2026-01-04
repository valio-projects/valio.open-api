using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Zod.Models;

namespace ValIO.Generators.TypeScript.Zod;

/// <summary>
/// Маппер типов OpenAPI на Zod типы
/// </summary>
public class ZodTypeMapper
{
    private readonly INamingConverter _namingConverter;

    public ZodTypeMapper(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }


    /// <summary>
    /// Маппит схему OpenAPI в Zod схему с зависимостями
    /// </summary>
    public ZodSchemaMappingResult MapToZodSchemaWithDependencies(
        IOpenApiSchema schema, 
        string propertyName, 
        ISchemaResolver schemaResolver,
        HashSet<string> processedSchemas,
        string? currentSchemaName = null)
    {
        var result = new ZodSchemaMappingResult();
        
        if (schema == null)
        {
            result.Expression = CreateAnyExpression();
            return result;
        }

        // Проверяем на ссылку на схему ДО разрешения
        // Используем унифицированный метод из SchemaCompositionHelper
        var referencedSchemaName = SchemaCompositionHelper.GetSchemaNameRaw(schema, schemaResolver);

        // Если есть ссылка на схему, используем её как зависимость
        if (!string.IsNullOrEmpty(referencedSchemaName) && referencedSchemaName != "object")
        {
            var typeName = _namingConverter.ToPascalCase(referencedSchemaName);
            var schemaName = $"{typeName}Schema";
            
            // Проверяем на циклическую ссылку - если схема ссылается сама на себя, используем z.lazy()
            var isCircular = !string.IsNullOrEmpty(currentSchemaName) && 
                            currentSchemaName.Equals(referencedSchemaName, StringComparison.OrdinalIgnoreCase);
            
            if (isCircular)
            {
                // Используем z.lazy() для циклических ссылок
                result.Expression = CreateLazyExpression(schemaName);
                // Не добавляем в зависимости, так как это та же схема
            }
            else
            {
                // Проверяем, является ли это enum (через resolved schema)
                var resolvedSchema = schemaResolver.ResolveSchema(schema);
                var isEnum = resolvedSchema.Enum != null && resolvedSchema.Enum.Count > 0;
                
                result.Expression = CreateReferenceExpression(schemaName, isEnum);
                
                if (isEnum)
                {
                    result.EnumDependencies.Add(schemaName);
                }
                else
                {
                    result.SchemaDependencies.Add(schemaName);
                }
            }
            
            return result;
        }

        var resolvedSchema2 = schemaResolver.ResolveSchema(schema);

        // Обрабатываем allOf - объединяем свойства из всех схем
        // Используем SchemaCompositionHelper для получения информации о композиции
        if (resolvedSchema2.AllOf != null && resolvedSchema2.AllOf.Count > 0)
        {
            return MapAllOfToZodSchema(resolvedSchema2.AllOf, schema, schemaResolver, processedSchemas, currentSchemaName);
        }

        // Обрабатываем oneOf - union тип
        if (resolvedSchema2.OneOf != null && resolvedSchema2.OneOf.Count > 0)
        {
            return MapOneOfToZodSchema(resolvedSchema2.OneOf, schemaResolver, processedSchemas, currentSchemaName);
        }

        // Обрабатываем anyOf - union тип (аналогично oneOf)
        if (resolvedSchema2.AnyOf != null && resolvedSchema2.AnyOf.Count > 0)
        {
            return MapAnyOfToZodSchema(resolvedSchema2.AnyOf, schemaResolver, processedSchemas, currentSchemaName);
        }

        // Обрабатываем массивы
        if (resolvedSchema2.Type.HasValue && resolvedSchema2.Type.Value.HasFlag(JsonSchemaType.Array))
        {
            if (resolvedSchema2.Items != null)
            {
                var itemResult = MapToZodSchemaWithDependencies(resolvedSchema2.Items, propertyName, schemaResolver, processedSchemas, currentSchemaName);
                result.Expression = CreateArrayExpression(itemResult.Expression);
                result.SchemaDependencies.UnionWith(itemResult.SchemaDependencies);
                result.EnumDependencies.UnionWith(itemResult.EnumDependencies);
                result.NestedSchemas.AddRange(itemResult.NestedSchemas);
            }
            else
            {
                result.Expression = CreateArrayExpression(CreateAnyExpression());
            }
            return result;
        }

        // Обрабатываем объекты
        if (resolvedSchema2.Type.HasValue && resolvedSchema2.Type.Value.HasFlag(JsonSchemaType.Object))
        {
            // Проверяем, есть ли у объекта ссылка на схему (проверяем оригинальную схему ДО разрешения)
            // Это важно для вложенных объектов, которые должны быть отдельными схемами
            // Используем унифицированный метод из SchemaCompositionHelper
            var objectSchemaName = SchemaCompositionHelper.GetSchemaNameRaw(schema, schemaResolver);
            
            // Если у объекта есть имя схемы и это не просто "object", создаем ссылку на схему
            if (!string.IsNullOrEmpty(objectSchemaName) && objectSchemaName != "object")
            {
                var typeName = _namingConverter.ToPascalCase(objectSchemaName);
                var schemaName = $"{typeName}Schema";
                
                // Проверяем на циклическую ссылку
                var isCircular = !string.IsNullOrEmpty(currentSchemaName) && 
                                currentSchemaName.Equals(objectSchemaName, StringComparison.OrdinalIgnoreCase);
                
                if (isCircular)
                {
                    // Используем z.lazy() для циклических ссылок
                    result.Expression = CreateLazyExpression(schemaName);
                    // Не добавляем в зависимости, так как это та же схема
                }
                else
                {
                    // Проверяем, является ли это enum
                    var isEnum = resolvedSchema2.Enum != null && resolvedSchema2.Enum.Count > 0;
                    result.Expression = CreateReferenceExpression(schemaName, isEnum);
                    
                    if (isEnum)
                    {
                        result.EnumDependencies.Add(schemaName);
                    }
                    else
                    {
                        result.SchemaDependencies.Add(schemaName);
                    }
                }
                
                return result;
            }
            
            // Если нет ссылки на схему, создаем inline схему
            if (resolvedSchema2.Properties != null && resolvedSchema2.Properties.Count > 0)
            {
                var properties = new Dictionary<string, ZodPropertyExpression>();
                foreach (var prop in resolvedSchema2.Properties)
                {
                    var propResult = MapToZodSchemaWithDependencies(prop.Value, prop.Key, schemaResolver, processedSchemas, currentSchemaName);
                    
                    var isRequired = resolvedSchema2.Required != null && resolvedSchema2.Required.Contains(prop.Key);
                    properties[prop.Key] = new ZodPropertyExpression
                    {
                        Expression = propResult.Expression,
                        IsRequired = isRequired
                    };
                    
                    // Собираем зависимости
                    result.SchemaDependencies.UnionWith(propResult.SchemaDependencies);
                    result.EnumDependencies.UnionWith(propResult.EnumDependencies);
                    result.NestedSchemas.AddRange(propResult.NestedSchemas);
                }
                
                result.Expression = CreateObjectExpression(properties);
            }
            else if (resolvedSchema2.AdditionalProperties != null)
            {
                var valueResult = MapToZodSchemaWithDependencies(resolvedSchema2.AdditionalProperties, propertyName, schemaResolver, processedSchemas, currentSchemaName);
                result.Expression = CreateRecordExpression(valueResult.Expression);
                result.SchemaDependencies.UnionWith(valueResult.SchemaDependencies);
                result.EnumDependencies.UnionWith(valueResult.EnumDependencies);
                result.NestedSchemas.AddRange(valueResult.NestedSchemas);
            }
            else
            {
                result.Expression = CreateRecordExpression(CreateAnyExpression());
            }
            return result;
        }

        // Обрабатываем примитивные типы
        if (resolvedSchema2.Type.HasValue)
        {
            var type = resolvedSchema2.Type.Value;
            var format = resolvedSchema2.Format?.ToLowerInvariant();
            var baseType = type & ~JsonSchemaType.Null;

            // Если есть enum значения, создаем отдельную enum схему
            if (resolvedSchema2.Enum != null && resolvedSchema2.Enum.Count > 0)
            {
                // Генерируем имя для enum схемы на основе контекста
                var enumSchemaName = GenerateEnumSchemaName(propertyName, resolvedSchema2, schemaResolver);
                var enumSchemaNamePascal = _namingConverter.ToPascalCase(enumSchemaName);
                var enumSchemaNameFull = $"{enumSchemaNamePascal}Schema";
                
                var enumValues = resolvedSchema2.Enum.Select(e => 
                {
                    object? value = e;
                    
                    // Обрабатываем JsonNode
                    if (value is JsonNode jsonNode)
                    {
                        return new ZodEnumValue
                        {
                            ValueString = jsonNode.GetValueKind() switch
                            {
                                System.Text.Json.JsonValueKind.String => $"\"{jsonNode.GetValue<string>()}\"",
                                System.Text.Json.JsonValueKind.Number => jsonNode.GetValue<decimal>().ToString(),
                                System.Text.Json.JsonValueKind.True => "true",
                                System.Text.Json.JsonValueKind.False => "false",
                                _ => "null"
                            }
                        };
                    }
                    
                    // Обрабатываем примитивные типы (не JsonNode)
                    if (value is string str)
                        return new ZodEnumValue { ValueString = $"\"{str}\"" };
                    if (value is int i)
                        return new ZodEnumValue { ValueString = i.ToString() };
                    if (value is long l)
                        return new ZodEnumValue { ValueString = l.ToString() };
                    if (value is decimal d)
                        return new ZodEnumValue { ValueString = d.ToString() };
                    if (value is double db)
                        return new ZodEnumValue { ValueString = db.ToString() };
                    if (value is float f)
                        return new ZodEnumValue { ValueString = f.ToString() };
                    if (value is bool b)
                        return new ZodEnumValue { ValueString = b ? "true" : "false" };
                    
                    return new ZodEnumValue { ValueString = value?.ToString() ?? "null" };
                }).ToList();
                
                // Определяем базовый тип enum
                var underlyingType = baseType == JsonSchemaType.Integer || baseType == JsonSchemaType.Number 
                    ? "number" 
                    : "string";
                
                result.Expression = CreateEnumExpression(enumSchemaNameFull, enumValues, underlyingType);
                result.EnumDependencies.Add(enumSchemaNameFull);
                
                // Сохраняем информацию о вложенной enum схеме
                result.NestedSchemas.Add(new ZodNestedSchemaInfo
                {
                    SchemaName = enumSchemaNameFull,
                    TypeName = enumSchemaNamePascal,
                    Description = resolvedSchema2.Description,
                    Schema = resolvedSchema2,
                    Properties = new List<ZodPropertySchemaTemplateData>() // Enum не имеет свойств
                });
                
                return result;
            }

            // Создаем примитивное выражение
            var primitiveType = (baseType, format) switch
            {
                (JsonSchemaType.Boolean, _) => ZodPrimitiveType.Boolean,
                (JsonSchemaType.Integer, _) => ZodPrimitiveType.Number,
                (JsonSchemaType.Number, _) => ZodPrimitiveType.Number,
                (JsonSchemaType.String, "binary") => ZodPrimitiveType.InstanceOf,
                (JsonSchemaType.String, _) => ZodPrimitiveType.String,
                _ => ZodPrimitiveType.Any
            };

            // Для integer сохраняем формат (int32, int64) для правильного рендеринга
            var formatForExpression = baseType == JsonSchemaType.Integer ? format : format;
            
            // Создаем ограничения валидации
            var validations = CreateValidationConstraints(resolvedSchema2);
            
            result.Expression = CreatePrimitiveExpression(primitiveType, formatForExpression, validations);
            return result;
        }

        result.Expression = CreateAnyExpression();
        return result;
    }

    /// <summary>
    /// Маппит allOf схему в Zod схему (объединение свойств из всех схем)
    /// Использует SchemaCompositionHelper для унификации логики
    /// </summary>
    private ZodSchemaMappingResult MapAllOfToZodSchema(
        IList<IOpenApiSchema> allOfSchemas,
        IOpenApiSchema originalSchema,
        ISchemaResolver schemaResolver,
        HashSet<string> processedSchemas,
        string? currentSchemaName)
    {
        var result = new ZodSchemaMappingResult();
        var allProperties = new Dictionary<string, IOpenApiSchema>();
        var allRequired = new HashSet<string>();

        // Разрешаем оригинальную схему для получения resolved версии
        var resolvedSchema = schemaResolver.ResolveSchema(originalSchema);
        
        // Создаем информацию о композиции для использования унифицированных методов
        var compositionInfo = SchemaCompositionHelper.CreateCompositionInfo(
            originalSchema, 
            resolvedSchema, 
            schemaResolver, 
            _namingConverter);
        
        if (compositionInfo != null && compositionInfo.CompositionType == SchemaCompositionType.AllOf)
        {
            // Используем информацию о композиции для обработки под-схем
            foreach (var subSchemaInfo in compositionInfo.SubSchemaInfos)
            {
                // Если под-схема - это ссылка на компонент, добавляем её как зависимость
                if (subSchemaInfo.IsComponentReference && !string.IsNullOrEmpty(subSchemaInfo.ReferencedSchemaName))
                {
                    var typeName = _namingConverter.ToPascalCase(subSchemaInfo.ReferencedSchemaName);
                    var schemaName = $"{typeName}Schema";
                    
                    // Проверяем на циклическую ссылку
                    var isCircular = !string.IsNullOrEmpty(currentSchemaName) && 
                                    currentSchemaName.Equals(subSchemaInfo.ReferencedSchemaName, StringComparison.OrdinalIgnoreCase);
                    
                    if (!isCircular)
                    {
                        result.SchemaDependencies.Add(schemaName);
                    }
                }
                
                // Обрабатываем свойства из под-схемы
                // Используем оригинальные свойства (до разрешения) для сохранения ссылок
                if (subSchemaInfo.OriginalProperties != null)
                {
                    foreach (var prop in subSchemaInfo.OriginalProperties)
                    {
                        allProperties[prop.Key] = prop.Value;
                    }
                }
                
                // Собираем required свойства
                if (subSchemaInfo.ResolvedSchema.Required != null)
                {
                    foreach (var req in subSchemaInfo.ResolvedSchema.Required)
                    {
                        allRequired.Add(req);
                    }
                }
            }
            
            // Добавляем required из самой схемы
            var mergedRequired = SchemaCompositionHelper.MergeRequiredProperties(compositionInfo, resolvedSchema, schemaResolver);
            foreach (var req in mergedRequired)
            {
                allRequired.Add(req);
            }
        }
        else
        {
            // Fallback на старую логику, если не удалось создать CompositionInfo
            foreach (var subSchema in allOfSchemas)
            {
                var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                var referencedSchemaName = SchemaCompositionHelper.GetSchemaNameRaw(subSchema, schemaResolver);

                if (!string.IsNullOrEmpty(referencedSchemaName) && referencedSchemaName != "object")
                {
                    var typeName = _namingConverter.ToPascalCase(referencedSchemaName);
                    var schemaName = $"{typeName}Schema";
                    
                    var isCircular = !string.IsNullOrEmpty(currentSchemaName) && 
                                    currentSchemaName.Equals(referencedSchemaName, StringComparison.OrdinalIgnoreCase);
                    
                    if (!isCircular)
                    {
                        result.SchemaDependencies.Add(schemaName);
                    }
                }
                
                var originalSubSchemaProperties = SchemaCompositionHelper.GetOriginalProperties(subSchema, resolvedSubSchema);
                if (originalSubSchemaProperties != null)
                {
                    foreach (var prop in originalSubSchemaProperties)
                    {
                        allProperties[prop.Key] = prop.Value;
                    }
                }
                
                if (resolvedSubSchema.Required != null)
                {
                    foreach (var req in resolvedSubSchema.Required)
                    {
                        allRequired.Add(req);
                    }
                }
            }
        }

        // Создаем объект с объединенными свойствами
        var properties = new Dictionary<string, ZodPropertyExpression>();
        foreach (var prop in allProperties)
        {
            var propResult = MapToZodSchemaWithDependencies(
                prop.Value,
                prop.Key,
                schemaResolver,
                processedSchemas,
                currentSchemaName);
            
            var isRequired = allRequired.Contains(prop.Key);
            properties[prop.Key] = new ZodPropertyExpression
            {
                Expression = propResult.Expression,
                IsRequired = isRequired
            };
            
            // Собираем зависимости
            result.SchemaDependencies.UnionWith(propResult.SchemaDependencies);
            result.EnumDependencies.UnionWith(propResult.EnumDependencies);
            result.NestedSchemas.AddRange(propResult.NestedSchemas);
        }
        
        result.Expression = CreateObjectExpression(properties);
        return result;
    }

    /// <summary>
    /// Маппит oneOf схему в Zod схему (union тип)
    /// </summary>
    private ZodSchemaMappingResult MapOneOfToZodSchema(
        IList<IOpenApiSchema> oneOfSchemas,
        ISchemaResolver schemaResolver,
        HashSet<string> processedSchemas,
        string? currentSchemaName)
    {
        var result = new ZodSchemaMappingResult();
        var unionExpressions = new List<ZodSchemaExpression>();

        foreach (var subSchema in oneOfSchemas)
        {
            var subResult = MapToZodSchemaWithDependencies(
                subSchema,
                "item",
                schemaResolver,
                processedSchemas,
                currentSchemaName);
            
            unionExpressions.Add(subResult.Expression);
            
            // Собираем зависимости
            result.SchemaDependencies.UnionWith(subResult.SchemaDependencies);
            result.EnumDependencies.UnionWith(subResult.EnumDependencies);
            result.NestedSchemas.AddRange(subResult.NestedSchemas);
        }

        result.Expression = CreateUnionExpression(unionExpressions);
        return result;
    }

    /// <summary>
    /// Маппит anyOf схему в Zod схему (union тип, аналогично oneOf)
    /// </summary>
    private ZodSchemaMappingResult MapAnyOfToZodSchema(
        IList<IOpenApiSchema> anyOfSchemas,
        ISchemaResolver schemaResolver,
        HashSet<string> processedSchemas,
        string? currentSchemaName)
    {
        // anyOf обрабатывается аналогично oneOf
        return MapOneOfToZodSchema(anyOfSchemas, schemaResolver, processedSchemas, currentSchemaName);
    }

    /// <summary>
    /// Генерирует имя для enum схемы на основе контекста
    /// </summary>
    private string GenerateEnumSchemaName(string propertyName, IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        // Пытаемся получить имя из схемы
        var schemaName = schemaResolver.GetSchemaName(schema, string.Empty);
        if (!string.IsNullOrEmpty(schemaName) && schemaName != "object")
        {
            return schemaName;
        }
        
        // Если имя не найдено, генерируем на основе имени свойства
        return $"{propertyName}Enum";
    }

    #region Helper Methods for Creating Expressions

    /// <summary>
    /// Создает выражение для z.any()
    /// </summary>
    private ZodSchemaExpression CreateAnyExpression()
    {
        return new ZodPrimitiveExpression { Type = ZodPrimitiveType.Any };
    }

    /// <summary>
    /// Создает выражение для ссылки на схему
    /// </summary>
    private ZodSchemaExpression CreateReferenceExpression(string schemaName, bool isEnum)
    {
        return new ZodReferenceExpression
        {
            SchemaName = schemaName,
            IsEnum = isEnum
        };
    }

    /// <summary>
    /// Создает выражение для z.lazy()
    /// </summary>
    private ZodSchemaExpression CreateLazyExpression(string schemaName)
    {
        return new ZodLazyExpression { SchemaName = schemaName };
    }

    /// <summary>
    /// Создает выражение для примитивного типа
    /// </summary>
    private ZodSchemaExpression CreatePrimitiveExpression(
        ZodPrimitiveType type, 
        string? format = null,
        ZodValidationConstraints? validations = null)
    {
        return new ZodPrimitiveExpression
        {
            Type = type,
            Format = format,
            Validations = validations
        };
    }

    /// <summary>
    /// Создает выражение для z.object({...})
    /// </summary>
    private ZodSchemaExpression CreateObjectExpression(Dictionary<string, ZodPropertyExpression> properties)
    {
        return new ZodObjectExpression { Properties = properties };
    }

    /// <summary>
    /// Создает выражение для z.array(...)
    /// </summary>
    private ZodSchemaExpression CreateArrayExpression(ZodSchemaExpression itemExpression)
    {
        return new ZodArrayExpression { ItemExpression = itemExpression };
    }

    /// <summary>
    /// Создает выражение для z.union([...])
    /// </summary>
    private ZodSchemaExpression CreateUnionExpression(List<ZodSchemaExpression> expressions)
    {
        return new ZodUnionExpression { Expressions = expressions };
    }

    /// <summary>
    /// Создает выражение для z.record(z.string(), ...)
    /// </summary>
    private ZodSchemaExpression CreateRecordExpression(ZodSchemaExpression valueExpression)
    {
        return new ZodRecordExpression { ValueExpression = valueExpression };
    }

    /// <summary>
    /// Создает выражение для enum
    /// </summary>
    private ZodSchemaExpression CreateEnumExpression(
        string? schemaName,
        List<ZodEnumValue>? values,
        string underlyingType)
    {
        return new ZodEnumExpression
        {
            SchemaName = schemaName,
            Values = values,
            UnderlyingType = underlyingType
        };
    }

    /// <summary>
    /// Создает ограничения валидации из OpenAPI схемы
    /// </summary>
    private ZodValidationConstraints? CreateValidationConstraints(IOpenApiSchema schema)
    {
        var hasConstraints = !string.IsNullOrEmpty(schema.Minimum) ||
                            !string.IsNullOrEmpty(schema.Maximum) ||
                            schema.MinLength.HasValue ||
                            schema.MaxLength.HasValue ||
                            !string.IsNullOrEmpty(schema.Pattern);

        if (!hasConstraints)
            return null;

        return new ZodValidationConstraints
        {
            Minimum = schema.Minimum,
            Maximum = schema.Maximum,
            MinLength = schema.MinLength,
            MaxLength = schema.MaxLength,
            Pattern = schema.Pattern
        };
    }

    #endregion
}
