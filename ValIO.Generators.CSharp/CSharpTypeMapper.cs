using System.Text;
using System.Text.Json;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;

namespace ValIO.Generators.CSharp;

/// <summary>
/// Маппер типов OpenAPI на типы C#
/// </summary>
public class CSharpTypeMapper : ITypeMapper
{
    private readonly INamingConverter _namingConverter;

    public CSharpTypeMapper(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public string MapType(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return "object";

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        return MapTypeInternal(resolvedSchema, schemaResolver, false);
    }

    public string MapPropertyType(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return "object?";

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
                return isNullable ? $"{typeName}?" : typeName;
            }
        }

        // Обрабатываем allOf (наследование)
        if (schema.AllOf != null && schema.AllOf.Count > 0)
        {
            // Берем последнюю схему из allOf как основную
            var lastSchema = schema.AllOf.Last();
            var resolvedLastSchema = schemaResolver.ResolveSchema(lastSchema);
            
            // Если последняя схема - ссылка, используем её имя
            if (resolvedLastSchema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> lastRefHolder)
            {
                var lastRef = lastRefHolder.Reference;
                if (lastRef != null && !string.IsNullOrEmpty(lastRef.Id))
                {
                    var typeName = _namingConverter.ToPascalCase(lastRef.Id);
                    return isNullable ? $"{typeName}?" : typeName;
                }
            }
            
            // Иначе обрабатываем как обычную схему
            return MapTypeInternal(resolvedLastSchema, schemaResolver, isNullable);
        }

        // Обрабатываем oneOf/anyOf (union types)
        if (schema.OneOf != null && schema.OneOf.Count > 0)
        {
            // Для C# используем object или создаем union type
            return isNullable ? "object?" : "object";
        }

        if (schema.AnyOf != null && schema.AnyOf.Count > 0)
        {
            return isNullable ? "object?" : "object";
        }

        // Обрабатываем массивы
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Array))
        {
            if (schema.Items != null)
            {
                var itemType = MapTypeInternal(schema.Items, schemaResolver, false);
                return $"List<{itemType}>";
            }
            return "List<object>";
        }

        // Обрабатываем объекты
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Object))
        {
            // Если есть свойства, это объект
            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                return isNullable ? "object?" : "object";
            }

            // Если есть дополнительные свойства (dictionary)
            if (schema.AdditionalProperties != null)
            {
                var valueType = MapTypeInternal(schema.AdditionalProperties, schemaResolver, false);
                return $"Dictionary<string, {valueType}>";
            }

            return isNullable ? "object?" : "object";
        }

        // Обрабатываем примитивные типы
        if (schema.Type.HasValue)
        {
            var type = schema.Type.Value;
            var format = schema.Format?.ToLowerInvariant();

            // Убираем Null из типа для обработки
            var baseType = type & ~JsonSchemaType.Null;

            var csharpType = (baseType, format) switch
            {
                (JsonSchemaType.Boolean, _) => "bool",
                (JsonSchemaType.Integer, "int32") => "int",
                (JsonSchemaType.Integer, "int64") => "long",
                (JsonSchemaType.Integer, _) => "int",
                (JsonSchemaType.Number, "float") => "float",
                (JsonSchemaType.Number, "double") => "double",
                (JsonSchemaType.Number, "decimal") => "decimal",
                (JsonSchemaType.Number, _) => "double",
                (JsonSchemaType.String, "byte") => "byte[]",
                (JsonSchemaType.String, "binary") => "byte[]",
                (JsonSchemaType.String, "date") => "DateTime",
                (JsonSchemaType.String, "date-time") => "DateTimeOffset",
                (JsonSchemaType.String, "uuid") => "Guid",
                (JsonSchemaType.String, "uri") => "Uri",
                (JsonSchemaType.String, _) => "string",
                _ => "object"
            };

            return isNullable && csharpType != "string" && csharpType != "byte[]" && csharpType != "object" 
                ? $"{csharpType}?" 
                : csharpType;
        }

        // Если тип не определен, возвращаем object
        return isNullable ? "object?" : "object";
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

