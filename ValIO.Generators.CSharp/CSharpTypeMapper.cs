using System.Text;
using System.Text.Json;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Generators.CSharp.Models;

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

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo с мета-данными
    /// </summary>
    public TypeInfo MapTypeInfo(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return TypeInfo.Simple("object");

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        return MapTypeInfoInternal(resolvedSchema, schemaResolver, false);
    }

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo для свойства с учетом nullable
    /// </summary>
    public TypeInfo MapPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return TypeInfo.Nullable(TypeInfo.Simple("object"));

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        var isNullable = !isRequired || IsSchemaNullable(resolvedSchema);
        return MapTypeInfoInternal(resolvedSchema, schemaResolver, isNullable);
    }

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo для form-data свойства
    /// </summary>
    public TypeInfo MapFormDataPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return TypeInfo.Simple("string", isPrimitive: true);

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        var isNullable = !isRequired;
        
        // Проверяем, является ли это файлом
        var isFile = resolvedSchema.Format?.ToLowerInvariant() == "binary";
        
        // Проверяем, является ли это массивом
        var isArray = resolvedSchema.Type.HasValue && resolvedSchema.Type.Value.HasFlag(JsonSchemaType.Array);
        
        if (isFile)
        {
            if (isArray)
            {
                return TypeInfo.Collection("List", TypeInfo.Simple("Stream", isFile: true), isNullable);
            }
            return TypeInfo.Simple("Stream", isFile: true);
        }
        
        if (isArray && resolvedSchema.Items != null)
        {
            var itemTypeInfo = MapFormDataPropertyTypeInfo(resolvedSchema.Items, false, schemaResolver);
            return TypeInfo.Collection("List", itemTypeInfo, isNullable);
        }
        
        // Маппим примитивные типы для form-data
        var typeInfo = MapTypeInfoInternal(resolvedSchema, schemaResolver, isNullable);
        return typeInfo;
    }

    /// <summary>
    /// Внутренний метод для маппинга схемы в TypeInfo
    /// </summary>
    private TypeInfo MapTypeInfoInternal(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable)
    {
        // Проверяем на ссылку на схему
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

        // Обрабатываем allOf (наследование)
        if (schema.AllOf != null && schema.AllOf.Count > 0)
        {
            string? baseTypeName = null;
            foreach (var subSchema in schema.AllOf)
            {
                var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                if (resolvedSubSchema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder)
                {
                    var reference = refHolder.Reference;
                    if (reference != null && !string.IsNullOrEmpty(reference.Id))
                    {
                        baseTypeName = _namingConverter.ToPascalCase(reference.Id);
                        break;
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(baseTypeName))
            {
                var typeInfo = TypeInfo.Simple(baseTypeName);
                return isNullable ? TypeInfo.Nullable(typeInfo) : typeInfo;
            }
            
            var lastSchema = schema.AllOf.Last();
            var resolvedLastSchema = schemaResolver.ResolveSchema(lastSchema);
            return MapTypeInfoInternal(resolvedLastSchema, schemaResolver, isNullable);
        }

        // Обрабатываем oneOf/anyOf
        if ((schema.OneOf != null && schema.OneOf.Count > 0) || (schema.AnyOf != null && schema.AnyOf.Count > 0))
        {
            var schemas = schema.OneOf ?? schema.AnyOf ?? Array.Empty<IOpenApiSchema>();
            var commonBaseType = FindCommonBaseType(schemas, schemaResolver);
            if (!string.IsNullOrEmpty(commonBaseType))
            {
                var typeInfo = TypeInfo.Simple(commonBaseType);
                return isNullable ? TypeInfo.Nullable(typeInfo) : typeInfo;
            }
            
            var typeInfoObj = TypeInfo.Simple("object");
            return isNullable ? TypeInfo.Nullable(typeInfoObj) : typeInfoObj;
        }

        // Обрабатываем массивы
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Array))
        {
            if (schema.Items != null)
            {
                var itemTypeInfo = MapTypeInfoInternal(schema.Items, schemaResolver, false);
                return TypeInfo.Collection("List", itemTypeInfo, isNullable);
            }
            return TypeInfo.Collection("List", TypeInfo.Simple("object"), isNullable);
        }

        // Обрабатываем объекты
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Object))
        {
            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                var typeInfo = TypeInfo.Simple("object");
                return isNullable ? TypeInfo.Nullable(typeInfo) : typeInfo;
            }

            if (schema.AdditionalProperties != null)
            {
                var valueTypeInfo = MapTypeInfoInternal(schema.AdditionalProperties, schemaResolver, false);
                var keyTypeInfo = TypeInfo.Simple("string", isPrimitive: true);
                return TypeInfo.Dictionary("Dictionary", keyTypeInfo, valueTypeInfo, isNullable);
            }

            if (schema.AdditionalPropertiesAllowed)
            {
                var valueTypeInfo = TypeInfo.Simple("object");
                var keyTypeInfo = TypeInfo.Simple("string", isPrimitive: true);
                return TypeInfo.Dictionary("Dictionary", keyTypeInfo, valueTypeInfo, isNullable);
            }

            var typeInfoObj = TypeInfo.Simple("object");
            return isNullable ? TypeInfo.Nullable(typeInfoObj) : typeInfoObj;
        }

        // Обрабатываем примитивные типы
        if (schema.Type.HasValue)
        {
            var type = schema.Type.Value;
            var format = schema.Format?.ToLowerInvariant();
            var baseType = type & ~JsonSchemaType.Null;

            var (baseTypeName, isPrimitive, isFile) = (baseType, format) switch
            {
                (JsonSchemaType.Boolean, _) => ("bool", true, false),
                (JsonSchemaType.Integer, "int32") => ("int", true, false),
                (JsonSchemaType.Integer, "int64") => ("long", true, false),
                (JsonSchemaType.Integer, _) => ("int", true, false),
                (JsonSchemaType.Number, "float") => ("float", true, false),
                (JsonSchemaType.Number, "double") => ("double", true, false),
                (JsonSchemaType.Number, "decimal") => ("decimal", true, false),
                (JsonSchemaType.Number, _) => ("double", true, false),
                (JsonSchemaType.String, "byte") => ("byte[]", true, true),
                (JsonSchemaType.String, "binary") => ("byte[]", true, true),
                (JsonSchemaType.String, "date") => ("DateTime", true, false),
                (JsonSchemaType.String, "date-time") => ("DateTimeOffset", true, false),
                (JsonSchemaType.String, "uuid") => ("Guid", true, false),
                (JsonSchemaType.String, "uri") => ("Uri", true, false),
                (JsonSchemaType.String, _) => ("string", true, false),
                _ => ("object", false, false)
            };

            var typeInfo = TypeInfo.Simple(baseTypeName, isPrimitive, isFile);
            
            // Для string, byte[], object nullable не добавляется через "?"
            if (isNullable && baseTypeName != "string" && baseTypeName != "byte[]" && baseTypeName != "object")
            {
                return TypeInfo.Nullable(typeInfo);
            }
            
            return typeInfo;
        }

        // Если тип не определен, возвращаем object
        var defaultTypeInfo = TypeInfo.Simple("object");
        return isNullable ? TypeInfo.Nullable(defaultTypeInfo) : defaultTypeInfo;
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
        // allOf означает, что схема должна включать все свойства из всех указанных схем
        // В C# это обычно реализуется через наследование или композицию
        if (schema.AllOf != null && schema.AllOf.Count > 0)
        {
            // Ищем первую схему-ссылку как базовый класс
            // Остальные схемы добавляют дополнительные свойства
            string? baseTypeName = null;
            foreach (var subSchema in schema.AllOf)
            {
                var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                if (resolvedSubSchema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder)
                {
                    var reference = refHolder.Reference;
                    if (reference != null && !string.IsNullOrEmpty(reference.Id))
                    {
                        baseTypeName = _namingConverter.ToPascalCase(reference.Id);
                        break; // Берем первую ссылку как базовый класс
                    }
                }
            }
            
            // Если нашли базовый класс, используем его
            if (!string.IsNullOrEmpty(baseTypeName))
            {
                return isNullable ? $"{baseTypeName}?" : baseTypeName;
            }
            
            // Если нет ссылок, берем последнюю схему как основную
            var lastSchema = schema.AllOf.Last();
            var resolvedLastSchema = schemaResolver.ResolveSchema(lastSchema);
            return MapTypeInternal(resolvedLastSchema, schemaResolver, isNullable);
        }

        // Обрабатываем oneOf (union type - ровно один из типов)
        // В C# нет нативных union types, используем object или пытаемся найти общий базовый тип
        if (schema.OneOf != null && schema.OneOf.Count > 0)
        {
            // Пытаемся найти общий базовый тип среди всех схем
            var commonBaseType = FindCommonBaseType(schema.OneOf, schemaResolver);
            if (!string.IsNullOrEmpty(commonBaseType))
            {
                return isNullable ? $"{commonBaseType}?" : commonBaseType;
            }
            
            // Если не нашли общий тип, используем object
            return isNullable ? "object?" : "object";
        }

        // Обрабатываем anyOf (union type - один или несколько из типов)
        // В C# нет нативных union types, используем object или пытаемся найти общий базовый тип
        if (schema.AnyOf != null && schema.AnyOf.Count > 0)
        {
            // Пытаемся найти общий базовый тип среди всех схем
            var commonBaseType = FindCommonBaseType(schema.AnyOf, schemaResolver);
            if (!string.IsNullOrEmpty(commonBaseType))
            {
                return isNullable ? $"{commonBaseType}?" : commonBaseType;
            }
            
            // Если не нашли общий тип, используем object
            return isNullable ? "object?" : "object";
        }

        // Обрабатываем массивы
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Array))
        {
            if (schema.Items != null)
            {
                var itemType = MapTypeInternal(schema.Items, schemaResolver, false);
                // Для массивов nullable применяется к самому массиву, а не к элементам
                // В C# List<T>? означает nullable список, но элементы могут быть nullable отдельно
                return isNullable ? $"List<{itemType}>?" : $"List<{itemType}>";
            }
            return isNullable ? "List<object>?" : "List<object>";
        }

        // Обрабатываем объекты
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Object))
        {
            // Если есть свойства, это объект (даже если есть дополнительные свойства)
            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                return isNullable ? "object?" : "object";
            }

            // Если нет свойств, но есть дополнительные свойства - это dictionary
            if (schema.AdditionalProperties != null)
            {
                var valueType = MapTypeInternal(schema.AdditionalProperties, schemaResolver, false);
                // Для словарей nullable применяется к самому словарю
                return isNullable ? $"Dictionary<string, {valueType}>?" : $"Dictionary<string, {valueType}>";
            }

            // Если additionalPropertiesAllowed = true и нет схемы - это Dictionary<string, object>
            // По умолчанию AdditionalPropertiesAllowed = true в OpenAPI
            // Если явно установлено false, то дополнительные свойства запрещены
            if (schema.AdditionalPropertiesAllowed)
            {
                // Если нет явной схемы для дополнительных свойств, используем object
                return isNullable ? "Dictionary<string, object>?" : "Dictionary<string, object>";
            }

            // Если дополнительные свойства запрещены и нет свойств - это пустой объект
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

    /// <summary>
    /// Пытается найти общий базовый тип среди схем oneOf/anyOf
    /// </summary>
    private string? FindCommonBaseType(IList<IOpenApiSchema> schemas, ISchemaResolver schemaResolver)
    {
        if (schemas == null || schemas.Count == 0)
            return null;

        // Собираем все типы из ссылок
        var typeNames = new List<string>();
        foreach (var subSchema in schemas)
        {
            var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
            if (resolvedSubSchema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder)
            {
                var reference = refHolder.Reference;
                if (reference != null && !string.IsNullOrEmpty(reference.Id))
                {
                    var typeName = _namingConverter.ToPascalCase(reference.Id);
                    typeNames.Add(typeName);
                }
            }
        }

        // Если все схемы - ссылки на один и тот же тип, возвращаем его
        if (typeNames.Count == schemas.Count && typeNames.Distinct().Count() == 1)
        {
            return typeNames.First();
        }

        // Если все схемы - ссылки, но разные типы, возвращаем null (будет использован object)
        // В будущем можно добавить логику поиска общего базового класса через анализ схем
        return null;
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

