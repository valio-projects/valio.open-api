using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
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
    public string MapToZodSchema(IOpenApiSchema schema, ApiProperty property, ISchemaResolver schemaResolver)
    {
        var result = MapToZodSchemaWithDependencies(schema, property, schemaResolver, new HashSet<string>(), null);
        return result.ZodSchema;
    }

    /// <summary>
    /// Маппит схему OpenAPI в Zod схему с зависимостями
    /// </summary>
    public ZodSchemaMappingResult MapToZodSchemaWithDependencies(
        IOpenApiSchema schema, 
        ApiProperty property, 
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
        string? referencedSchemaName = null;
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> referenceHolder)
        {
            var reference = referenceHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                referencedSchemaName = reference.Id;
            }
        }

        // Также проверяем через GetSchemaName
        if (string.IsNullOrEmpty(referencedSchemaName))
        {
            referencedSchemaName = schemaResolver.GetSchemaName(schema, string.Empty);
        }

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

        // Обрабатываем массивы
        if (resolvedSchema2.Type.HasValue && resolvedSchema2.Type.Value.HasFlag(JsonSchemaType.Array))
        {
            if (resolvedSchema2.Items != null)
            {
                var itemResult = MapToZodSchemaWithDependencies(resolvedSchema2.Items, property, schemaResolver, processedSchemas, currentSchemaName);
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
            string? objectSchemaName = null;
            if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> objectRefHolder)
            {
                var objectRef = objectRefHolder.Reference;
                if (objectRef != null && !string.IsNullOrEmpty(objectRef.Id))
                {
                    objectSchemaName = objectRef.Id;
                }
            }
            
            if (string.IsNullOrEmpty(objectSchemaName))
            {
                objectSchemaName = schemaResolver.GetSchemaName(schema, string.Empty);
            }
            
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
                    var propResult = MapToZodSchemaWithDependencies(prop.Value, new ApiProperty { Name = prop.Key }, schemaResolver, processedSchemas, currentSchemaName);
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
                var valueResult = MapToZodSchemaWithDependencies(resolvedSchema2.AdditionalProperties, property, schemaResolver, processedSchemas, currentSchemaName);
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

            // Добавляем валидацию на основе ограничений
            if (property.Min.HasValue)
            {
                zodType += $".min({property.Min})";
            }
            if (property.Max.HasValue)
            {
                zodType += $".max({property.Max})";
            }
            if (property.MinLength.HasValue)
            {
                zodType += $".min({property.MinLength})";
            }
            if (property.MaxLength.HasValue)
            {
                zodType += $".max({property.MaxLength})";
            }
            if (!string.IsNullOrEmpty(property.Pattern))
            {
                zodType += $".regex(/{property.Pattern}/)";
            }
            
            // Если есть enum значения, создаем отдельную enum схему
            if (resolvedSchema2.Enum != null && resolvedSchema2.Enum.Count > 0)
            {
                // Генерируем имя для enum схемы на основе контекста
                var enumSchemaName = GenerateEnumSchemaName(property, resolvedSchema2, schemaResolver);
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
    /// Генерирует имя для enum схемы на основе контекста
    /// </summary>
    private string GenerateEnumSchemaName(ApiProperty property, IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        // Пытаемся получить имя из схемы
        var schemaName = schemaResolver.GetSchemaName(schema, string.Empty);
        if (!string.IsNullOrEmpty(schemaName) && schemaName != "object")
        {
            return schemaName;
        }
        
        // Если имя не найдено, генерируем на основе имени свойства
        return $"{property.Name}Enum";
    }
}
