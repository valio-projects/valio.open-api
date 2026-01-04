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
    /// Маппит схему OpenAPI в Zod схему (старый метод для обратной совместимости)
    /// </summary>
    public string MapToZodSchema(IOpenApiSchema schema, string propertyName, ISchemaResolver schemaResolver)
    {
        var result = MapToZodSchemaWithDependencies(schema, propertyName, schemaResolver, new HashSet<string>(), null);
        return result.ZodSchema;
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
            result.ZodSchema = "z.any()";
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
                result.ZodSchema = $"z.lazy(() => {schemaName})";
                // Не добавляем в зависимости, так как это та же схема
            }
            else
            {
                result.ZodSchema = schemaName;
                
                // Проверяем, является ли это enum (через resolved schema)
                var resolvedSchema = schemaResolver.ResolveSchema(schema);
                if (resolvedSchema.Enum != null && resolvedSchema.Enum.Count > 0)
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
                result.ZodSchema = $"z.array({itemResult.ZodSchema})";
                result.SchemaDependencies.UnionWith(itemResult.SchemaDependencies);
                result.EnumDependencies.UnionWith(itemResult.EnumDependencies);
                result.NestedSchemas.AddRange(itemResult.NestedSchemas);
            }
            else
            {
                result.ZodSchema = "z.array(z.any())";
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
                    result.ZodSchema = $"z.lazy(() => {schemaName})";
                    // Не добавляем в зависимости, так как это та же схема
                }
                else
                {
                    result.ZodSchema = schemaName;
                    
                    // Проверяем, является ли это enum
                    if (resolvedSchema2.Enum != null && resolvedSchema2.Enum.Count > 0)
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
                var props = new List<string>();
                foreach (var prop in resolvedSchema2.Properties)
                {
                    var propResult = MapToZodSchemaWithDependencies(prop.Value, prop.Key, schemaResolver, processedSchemas, currentSchemaName);
                    props.Add($"{_namingConverter.ToCamelCase(prop.Key)}: {propResult.ZodSchema}");
                    
                    // Собираем зависимости
                    result.SchemaDependencies.UnionWith(propResult.SchemaDependencies);
                    result.EnumDependencies.UnionWith(propResult.EnumDependencies);
                    result.NestedSchemas.AddRange(propResult.NestedSchemas);
                }
                result.ZodSchema = $"z.object({{{string.Join(", ", props)}}})";
            }
            else if (resolvedSchema2.AdditionalProperties != null)
            {
                var valueResult = MapToZodSchemaWithDependencies(resolvedSchema2.AdditionalProperties, propertyName, schemaResolver, processedSchemas, currentSchemaName);
                result.ZodSchema = $"z.record(z.string(), {valueResult.ZodSchema})";
                result.SchemaDependencies.UnionWith(valueResult.SchemaDependencies);
                result.EnumDependencies.UnionWith(valueResult.EnumDependencies);
                result.NestedSchemas.AddRange(valueResult.NestedSchemas);
            }
            else
            {
                result.ZodSchema = "z.record(z.string(), z.any())";
            }
            return result;
        }

        // Обрабатываем примитивные типы
        if (resolvedSchema2.Type.HasValue)
        {
            var type = resolvedSchema2.Type.Value;
            var format = resolvedSchema2.Format?.ToLowerInvariant();
            var baseType = type & ~JsonSchemaType.Null;

            var zodType = (baseType, format) switch
            {
                (JsonSchemaType.Boolean, _) => "z.boolean()",
                (JsonSchemaType.Integer, _) => "z.number().int()",
                (JsonSchemaType.Number, _) => "z.number()",
                (JsonSchemaType.String, "date") => "z.string().date()",
                (JsonSchemaType.String, "date-time") => "z.string().datetime()",
                (JsonSchemaType.String, "uuid") => "z.string().uuid()",
                (JsonSchemaType.String, "uri") => "z.string().url()",
                (JsonSchemaType.String, "email") => "z.string().email()",
                (JsonSchemaType.String, "binary") => "z.instanceof(Blob)",
                (JsonSchemaType.String, _) => "z.string()",
                _ => "z.any()"
            };

            // Добавляем валидацию на основе ограничений из схемы
            // Minimum и Maximum имеют тип string? в OpenAPI схеме
            if (!string.IsNullOrEmpty(resolvedSchema2.Minimum))
            {
                zodType += $".min({resolvedSchema2.Minimum})";
            }
            if (!string.IsNullOrEmpty(resolvedSchema2.Maximum))
            {
                zodType += $".max({resolvedSchema2.Maximum})";
            }
            // MinLength и MaxLength имеют тип int? в OpenAPI схеме
            if (resolvedSchema2.MinLength.HasValue)
            {
                zodType += $".min({resolvedSchema2.MinLength})";
            }
            if (resolvedSchema2.MaxLength.HasValue)
            {
                zodType += $".max({resolvedSchema2.MaxLength})";
            }
            if (!string.IsNullOrEmpty(resolvedSchema2.Pattern))
            {
                zodType += $".regex(/{resolvedSchema2.Pattern}/)";
            }
            
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
                        return jsonNode.GetValueKind() switch
                        {
                            System.Text.Json.JsonValueKind.String => $"\"{jsonNode.GetValue<string>()}\"",
                            System.Text.Json.JsonValueKind.Number => jsonNode.GetValue<decimal>().ToString(),
                            System.Text.Json.JsonValueKind.True => "true",
                            System.Text.Json.JsonValueKind.False => "false",
                            _ => "null"
                        };
                    }
                    
                    // Обрабатываем примитивные типы (не JsonNode)
                    if (value is string str)
                        return $"\"{str}\"";
                    if (value is int i)
                        return i.ToString();
                    if (value is long l)
                        return l.ToString();
                    if (value is decimal d)
                        return d.ToString();
                    if (value is double db)
                        return db.ToString();
                    if (value is float f)
                        return f.ToString();
                    if (value is bool b)
                        return b ? "true" : "false";
                    
                    return value?.ToString() ?? "null";
                }).ToList();
                
                zodType = $"{enumSchemaNameFull}";
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
            }

            result.ZodSchema = zodType;
            return result;
        }

        result.ZodSchema = "z.any()";
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
        var allProperties = new Dictionary<string, string>();
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
                        var propResult = MapToZodSchemaWithDependencies(
                            prop.Value,
                            prop.Key,
                            schemaResolver,
                            processedSchemas,
                            currentSchemaName);
                        
                        allProperties[prop.Key] = propResult.ZodSchema;
                        
                        // Собираем зависимости
                        result.SchemaDependencies.UnionWith(propResult.SchemaDependencies);
                        result.EnumDependencies.UnionWith(propResult.EnumDependencies);
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
                        var propResult = MapToZodSchemaWithDependencies(
                            prop.Value,
                            prop.Key,
                            schemaResolver,
                            processedSchemas,
                            currentSchemaName);
                        
                        allProperties[prop.Key] = propResult.ZodSchema;
                        
                        result.SchemaDependencies.UnionWith(propResult.SchemaDependencies);
                        result.EnumDependencies.UnionWith(propResult.EnumDependencies);
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

        // Генерируем Zod схему с объединенными свойствами
        var props = new List<string>();
        foreach (var prop in allProperties)
        {
            var camelCaseName = _namingConverter.ToCamelCase(prop.Key);
            var isRequired = allRequired.Contains(prop.Key);
            var optional = isRequired ? "" : ".optional()";
            props.Add($"{camelCaseName}: {prop.Value}{optional}");
        }
        
        result.ZodSchema = $"z.object({{{string.Join(", ", props)}}})";
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
        var unionSchemas = new List<string>();

        foreach (var subSchema in oneOfSchemas)
        {
            var subResult = MapToZodSchemaWithDependencies(
                subSchema,
                "item",
                schemaResolver,
                processedSchemas,
                currentSchemaName);
            
            unionSchemas.Add(subResult.ZodSchema);
            
            // Собираем зависимости
            result.SchemaDependencies.UnionWith(subResult.SchemaDependencies);
            result.EnumDependencies.UnionWith(subResult.EnumDependencies);
        }

        result.ZodSchema = $"z.union([{string.Join(", ", unionSchemas)}])";
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
}
