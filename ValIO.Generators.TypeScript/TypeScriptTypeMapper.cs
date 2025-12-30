using System.Text;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;

namespace ValIO.Generators.TypeScript;

/// <summary>
/// Маппер типов OpenAPI на типы TypeScript
/// </summary>
public class TypeScriptTypeMapper : ITypeMapper
{
    private readonly INamingConverter _namingConverter;

    public TypeScriptTypeMapper(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public string MapType(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return "any";

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        return MapTypeInternal(resolvedSchema, schemaResolver, false);
    }

    public string MapPropertyType(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return "any";

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        var isNullable = !isRequired || IsSchemaNullable(resolvedSchema);
        return MapTypeInternal(resolvedSchema, schemaResolver, isNullable);
    }

    public string MapEnumType(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return "string";

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        
        // Если есть enum значения, возвращаем имя типа enum
        if (resolvedSchema.Enum != null && resolvedSchema.Enum.Count > 0)
        {
            var schemaName = schemaResolver.GetSchemaName(resolvedSchema, "Enum");
            return _namingConverter.ToPascalCase(schemaName);
        }

        // Иначе возвращаем базовый тип
        return MapType(resolvedSchema, schemaResolver);
    }

    private string MapTypeInternal(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable)
    {
        // Проверяем на ссылку на схему
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> referenceHolder)
        {
            var reference = referenceHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                var typeName = _namingConverter.ToPascalCase(reference.Id);
                return typeName;
            }
        }

        // Обрабатываем allOf (наследование)
        if (schema.AllOf != null && schema.AllOf.Count > 0)
        {
            // Для TypeScript allOf означает intersection type
            var types = new List<string>();
            foreach (var subSchema in schema.AllOf)
            {
                var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                types.Add(MapTypeInternal(resolvedSubSchema, schemaResolver, false));
            }
            return string.Join(" & ", types);
        }

        // Обрабатываем oneOf (union type)
        if (schema.OneOf != null && schema.OneOf.Count > 0)
        {
            var types = new List<string>();
            foreach (var subSchema in schema.OneOf)
            {
                var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                types.Add(MapTypeInternal(resolvedSubSchema, schemaResolver, false));
            }
            return string.Join(" | ", types);
        }

        // Обрабатываем anyOf (union type)
        if (schema.AnyOf != null && schema.AnyOf.Count > 0)
        {
            var types = new List<string>();
            foreach (var subSchema in schema.AnyOf)
            {
                var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                types.Add(MapTypeInternal(resolvedSubSchema, schemaResolver, false));
            }
            return string.Join(" | ", types);
        }

        // Обрабатываем массивы
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Array))
        {
            if (schema.Items != null)
            {
                var itemType = MapTypeInternal(schema.Items, schemaResolver, false);
                return $"{itemType}[]";
            }
            return "any[]";
        }

        // Обрабатываем объекты
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Object))
        {
            // Если есть свойства, это объект
            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                return "Record<string, any>";
            }

            // Если есть дополнительные свойства (dictionary)
            if (schema.AdditionalProperties != null)
            {
                var valueType = MapTypeInternal(schema.AdditionalProperties, schemaResolver, false);
                return $"Record<string, {valueType}>";
            }

            return "Record<string, any>";
        }

        // Обрабатываем примитивные типы
        if (schema.Type.HasValue)
        {
            var type = schema.Type.Value;
            var format = schema.Format?.ToLowerInvariant();

            // Убираем Null из типа для обработки
            var baseType = type & ~JsonSchemaType.Null;

            var tsType = (baseType, format) switch
            {
                (JsonSchemaType.Boolean, _) => "boolean",
                (JsonSchemaType.Integer, _) => "number",
                (JsonSchemaType.Number, _) => "number",
                (JsonSchemaType.String, "date") => "string",
                (JsonSchemaType.String, "date-time") => "string",
                (JsonSchemaType.String, "uuid") => "string",
                (JsonSchemaType.String, "uri") => "string",
                (JsonSchemaType.String, "binary") => "Blob",
                (JsonSchemaType.String, _) => "string",
                _ => "any"
            };

            // Для TypeScript nullable типы обрабатываются через union с null или через optional properties
            return tsType;
        }

        // Если тип не определен, возвращаем any
        return "any";
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
            if (nullableExtension is JsonNodeExtension { Node: { } jsonNode } && jsonNode.GetValueKind() == System.Text.Json.JsonValueKind.True)
            {
                return true;
            }
        }

        return false;
    }
}

