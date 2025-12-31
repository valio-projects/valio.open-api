using System.Text;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Generators.CSharp.Models;

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

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo с мета-данными для TypeScript
    /// </summary>
    public TypeInfo MapTypeInfo(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return TypeInfo.Simple("any", isPrimitive: true);

        // Проверяем на ссылку ДО разрешения, чтобы сохранить имя схемы
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> referenceHolder)
        {
            var reference = referenceHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                var typeName = _namingConverter.ToPascalCase(reference.Id);
                return TypeInfo.Simple(typeName);
            }
        }

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        return MapTypeInfoInternal(resolvedSchema, schemaResolver, false);
    }

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo для свойства с учетом nullable
    /// </summary>
    public TypeInfo MapPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return TypeInfo.Simple("any", isPrimitive: true);

        // Проверяем на ссылку ДО разрешения, чтобы сохранить имя схемы
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> referenceHolder)
        {
            var reference = referenceHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                var typeName = _namingConverter.ToPascalCase(reference.Id);
                var typeInfo = TypeInfo.Simple(typeName);
                // Разрешаем схему только для проверки nullable
                var resolvedSchemaForNullable = schemaResolver.ResolveSchema(schema);
                var isNullableValue = !isRequired || IsSchemaNullable(resolvedSchemaForNullable);
                return isNullableValue ? TypeInfo.Nullable(typeInfo) : typeInfo;
            }
        }

        // Если это не ссылка, разрешаем схему и маппим как обычно
        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        var isNullable = !isRequired || IsSchemaNullable(resolvedSchema);
        return MapTypeInfoInternal(resolvedSchema, schemaResolver, isNullable);
    }

    /// <summary>
    /// Внутренний метод для маппинга схемы в TypeInfo для TypeScript
    /// </summary>
    private TypeInfo MapTypeInfoInternal(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable)
    {
        // Проверяем на ссылку на схему ДО разрешения
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> referenceHolder)
        {
            var reference = referenceHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                var typeName = _namingConverter.ToPascalCase(reference.Id);
                var typeInfo = TypeInfo.Simple(typeName);
                return isNullable ? TypeInfo.Nullable(typeInfo) : typeInfo;
            }
        }

        // Также проверяем через GetSchemaName
        var schemaName = schemaResolver.GetSchemaName(schema, string.Empty);
        if (!string.IsNullOrEmpty(schemaName) && schemaName != "object")
        {
            var typeName = _namingConverter.ToPascalCase(schemaName);
            var typeInfo = TypeInfo.Simple(typeName);
            return isNullable ? TypeInfo.Nullable(typeInfo) : typeInfo;
        }

        // Обрабатываем allOf (intersection type в TypeScript)
        if (schema.AllOf != null && schema.AllOf.Count > 0)
        {
            var types = new List<TypeInfo>();
            foreach (var subSchema in schema.AllOf)
            {
                var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                types.Add(MapTypeInfoInternal(resolvedSubSchema, schemaResolver, false));
            }
            // Для TypeScript allOf - это intersection, но TypeInfo не поддерживает это напрямую
            // Используем первый тип как базовый
            if (types.Count > 0)
            {
                return isNullable ? TypeInfo.Nullable(types[0]) : types[0];
            }
        }

        // Обрабатываем oneOf/anyOf (union type в TypeScript)
        if ((schema.OneOf != null && schema.OneOf.Count > 0) || (schema.AnyOf != null && schema.AnyOf.Count > 0))
        {
            var schemas = schema.OneOf ?? schema.AnyOf ?? Array.Empty<IOpenApiSchema>();
            var types = new List<TypeInfo>();
            foreach (var subSchema in schemas)
            {
                var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                types.Add(MapTypeInfoInternal(resolvedSubSchema, schemaResolver, false));
            }
            // Для TypeScript union - используем первый тип как базовый
            if (types.Count > 0)
            {
                return isNullable ? TypeInfo.Nullable(types[0]) : types[0];
            }
        }

        // Обрабатываем массивы
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Array))
        {
            if (schema.Items != null)
            {
                var itemTypeInfo = MapTypeInfoInternal(schema.Items, schemaResolver, false);
                // Для TypeScript массивы: T[]
                // Используем IsCollection для правильной обработки в CollectTypesFromTypeInfo
                var arrayType = TypeInfo.Collection("Array", itemTypeInfo);
                // Переопределяем FullTypeName для TypeScript формата
                arrayType.FullTypeName = $"{itemTypeInfo.FullTypeName}[]";
                return isNullable ? TypeInfo.Nullable(arrayType) : arrayType;
            }
            var anyArrayType = TypeInfo.Collection("Array", TypeInfo.Simple("any", isPrimitive: true));
            anyArrayType.FullTypeName = "any[]";
            return isNullable ? TypeInfo.Nullable(anyArrayType) : anyArrayType;
        }

        // Обрабатываем объекты
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Object))
        {
            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                // Пытаемся найти имя схемы
                var objectSchemaName = schemaResolver.GetSchemaName(schema, string.Empty);
                
                if (!string.IsNullOrEmpty(objectSchemaName) && objectSchemaName != "object")
                {
                    var typeName = _namingConverter.ToPascalCase(objectSchemaName);
                    var objectTypeInfo = TypeInfo.Simple(typeName);
                    return isNullable ? TypeInfo.Nullable(objectTypeInfo) : objectTypeInfo;
                }
                
                // Если имя не найдено, используем Record<string, any>
                var recordType = TypeInfo.Simple("Record<string, any>");
                return isNullable ? TypeInfo.Nullable(recordType) : recordType;
            }

            if (schema.AdditionalProperties != null)
            {
                var valueTypeInfo = MapTypeInfoInternal(schema.AdditionalProperties, schemaResolver, false);
                var keyTypeInfo = TypeInfo.Simple("string", isPrimitive: true);
                return TypeInfo.Dictionary("Record", keyTypeInfo, valueTypeInfo, isNullable);
            }

            if (schema.AdditionalPropertiesAllowed)
            {
                var valueTypeInfo = TypeInfo.Simple("any", isPrimitive: true);
                var keyTypeInfo = TypeInfo.Simple("string", isPrimitive: true);
                return TypeInfo.Dictionary("Record", keyTypeInfo, valueTypeInfo, isNullable);
            }

            var defaultObjectType = TypeInfo.Simple("Record<string, any>");
            return isNullable ? TypeInfo.Nullable(defaultObjectType) : defaultObjectType;
        }

        // Обрабатываем примитивные типы
        if (schema.Type.HasValue)
        {
            var type = schema.Type.Value;
            var format = schema.Format?.ToLowerInvariant();
            var baseType = type & ~JsonSchemaType.Null;

            var (baseTypeName, isPrimitive, isFile) = (baseType, format) switch
            {
                (JsonSchemaType.Boolean, _) => ("boolean", true, false),
                (JsonSchemaType.Integer, _) => ("number", true, false),
                (JsonSchemaType.Number, _) => ("number", true, false),
                (JsonSchemaType.String, "date") => ("string", true, false),
                (JsonSchemaType.String, "date-time") => ("string", true, false),
                (JsonSchemaType.String, "uuid") => ("string", true, false),
                (JsonSchemaType.String, "uri") => ("string", true, false),
                (JsonSchemaType.String, "binary") => ("Blob", true, true),
                (JsonSchemaType.String, _) => ("string", true, false),
                _ => ("any", false, false)
            };

            var typeInfo = TypeInfo.Simple(baseTypeName, isPrimitive, isFile);
            
            // Для TypeScript nullable типы обрабатываются через union с null или через optional properties
            // Но для TypeInfo мы все равно помечаем как nullable
            if (isNullable && baseTypeName != "any")
            {
                return TypeInfo.Nullable(typeInfo);
            }
            
            return typeInfo;
        }

        // Если тип не определен, возвращаем any
        var defaultTypeInfo = TypeInfo.Simple("any", isPrimitive: true);
        return isNullable ? TypeInfo.Nullable(defaultTypeInfo) : defaultTypeInfo;
    }
}

