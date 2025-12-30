using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;

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
    /// Маппит схему OpenAPI в Zod схему
    /// </summary>
    public string MapToZodSchema(IOpenApiSchema schema, ApiProperty property, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return "z.any()";

        // Проверяем на ссылку на схему ДО разрешения
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> referenceHolder)
        {
            var reference = referenceHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                var typeName = _namingConverter.ToPascalCase(reference.Id);
                return $"{typeName}Schema";
            }
        }

        // Также проверяем через GetSchemaName
        var schemaName = schemaResolver.GetSchemaName(schema, string.Empty);
        if (!string.IsNullOrEmpty(schemaName) && schemaName != "object")
        {
            var typeName = _namingConverter.ToPascalCase(schemaName);
            return $"{typeName}Schema";
        }

        var resolvedSchema = schemaResolver.ResolveSchema(schema);

        // Обрабатываем массивы
        if (resolvedSchema.Type.HasValue && resolvedSchema.Type.Value.HasFlag(JsonSchemaType.Array))
        {
            if (resolvedSchema.Items != null)
            {
                var itemSchema = MapToZodSchema(resolvedSchema.Items, property, schemaResolver);
                return $"z.array({itemSchema})";
            }
            return "z.array(z.any())";
        }

        // Обрабатываем объекты
        if (resolvedSchema.Type.HasValue && resolvedSchema.Type.Value.HasFlag(JsonSchemaType.Object))
        {
            if (resolvedSchema.Properties != null && resolvedSchema.Properties.Count > 0)
            {
                // Для вложенных объектов создаем inline схему
                var props = new List<string>();
                foreach (var prop in resolvedSchema.Properties)
                {
                    var propSchema = MapToZodSchema(prop.Value, new ApiProperty { Name = prop.Key }, schemaResolver);
                    props.Add($"{_namingConverter.ToCamelCase(prop.Key)}: {propSchema}");
                }
                return $"z.object({{{string.Join(", ", props)}}})";
            }

            if (resolvedSchema.AdditionalProperties != null)
            {
                var valueSchema = MapToZodSchema(resolvedSchema.AdditionalProperties, property, schemaResolver);
                return $"z.record(z.string(), {valueSchema})";
            }

            return "z.record(z.string(), z.any())";
        }

        // Обрабатываем примитивные типы
        if (resolvedSchema.Type.HasValue)
        {
            var type = resolvedSchema.Type.Value;
            var format = resolvedSchema.Format?.ToLowerInvariant();
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
            if (resolvedSchema.Enum != null && resolvedSchema.Enum.Count > 0)
            {
                var enumValues = resolvedSchema.Enum.Select(e => 
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
                });
                zodType = $"z.enum([{string.Join(", ", enumValues)}])";
            }

            return zodType;
        }

        return "z.any()";
    }
}
