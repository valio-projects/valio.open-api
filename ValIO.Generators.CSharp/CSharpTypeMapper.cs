using System.Text;
using System.Text.Json;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;

namespace ValIO.Generators.CSharp;

/// <summary>
/// Маппер типов OpenAPI на типы C#
/// </summary>
public class CSharpTypeMapper : BaseTypeMapper
{
    public CSharpTypeMapper(INamingConverter namingConverter)
        : base(namingConverter, GenerationLanguage.CSharp)
    {
    }

    public override string MapType(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return "object";

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        return MapTypeInternal(resolvedSchema, schemaResolver, false);
    }

    public override string MapPropertyType(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return "object?";

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        var isNullable = !isRequired || IsSchemaNullable(resolvedSchema);
        return MapTypeInternal(resolvedSchema, schemaResolver, isNullable);
    }

    public override string MapEnumType(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return "string";

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        
        // Если есть enum значения, возвращаем имя типа enum
        if (resolvedSchema.Enum != null && resolvedSchema.Enum.Count > 0)
        {
            var schemaName = schemaResolver.GetSchemaName(resolvedSchema, "Enum");
            return NamingConverter.ToPascalCase(schemaName);
        }

        // Иначе возвращаем базовый тип
        return MapType(resolvedSchema, schemaResolver);
    }

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo с мета-данными
    /// </summary>
    public override TypeInfo MapTypeInfo(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return TypeInfo.Simple("object", language: Language);

        // Важно: сначала проверяем ссылку на исходной схеме (до разрешения)
        // После разрешения ссылка может потеряться
        var typeName = ResolveSchemaReference(schema, schemaResolver);
        if (!string.IsNullOrEmpty(typeName))
        {
            return CreateReferenceTypeInfo(typeName, false);
        }

        // Если ссылки нет, разрешаем схему и обрабатываем дальше
        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        return MapTypeInfoInternal(resolvedSchema, schemaResolver, false);
    }

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo для свойства с учетом nullable
    /// </summary>
    public override TypeInfo MapPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return TypeInfo.Nullable(TypeInfo.Simple("object", language: Language));

        // Важно: сначала проверяем ссылку на исходной схеме (до разрешения)
        // После разрешения ссылка может потеряться
        var typeName = ResolveSchemaReference(schema, schemaResolver);
        if (!string.IsNullOrEmpty(typeName))
        {
            var resolvedSchema = schemaResolver.ResolveSchema(schema);
            var isNullable = !isRequired || IsSchemaNullable(resolvedSchema);
            return CreateReferenceTypeInfo(typeName, isNullable);
        }

        // Если ссылки нет, разрешаем схему и обрабатываем дальше
        var resolvedSchemaForNullable = schemaResolver.ResolveSchema(schema);
        var isNullableForProperty = !isRequired || IsSchemaNullable(resolvedSchemaForNullable);
        return MapTypeInfoInternal(resolvedSchemaForNullable, schemaResolver, isNullableForProperty);
    }

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo для form-data свойства
    /// </summary>
    public TypeInfo MapFormDataPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return TypeInfo.Simple("string", isPrimitive: true, language: GenerationLanguage.CSharp);

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
                return TypeInfo.Collection("List", TypeInfo.Simple("Stream", isFile: true, language: Language), isNullable, Language);
            }
            return TypeInfo.Simple("Stream", isFile: true, language: Language);
        }
        
        if (isArray && resolvedSchema.Items != null)
        {
            var itemTypeInfo = MapFormDataPropertyTypeInfo(resolvedSchema.Items, false, schemaResolver);
            return TypeInfo.Collection("List", itemTypeInfo, isNullable, Language);
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
        var typeName = ResolveSchemaReference(schema, schemaResolver);
        if (!string.IsNullOrEmpty(typeName))
        {
            return CreateReferenceTypeInfo(typeName, isNullable);
        }

        // Обрабатываем allOf (наследование)
        if (schema.AllOf != null && schema.AllOf.Count > 0)
        {
            return HandleAllOf(schema, schemaResolver, isNullable);
        }

        // Обрабатываем oneOf/anyOf
        if ((schema.OneOf != null && schema.OneOf.Count > 0) || (schema.AnyOf != null && schema.AnyOf.Count > 0))
        {
            return HandleOneOfAnyOf(schema, schemaResolver, isNullable);
        }

        // Обрабатываем массивы
        if (IsCollection(schema))
        {
            if (schema.Items != null)
            {
                var itemTypeInfo = MapTypeInfoInternal(schema.Items, schemaResolver, false);
                return CreateCollectionTypeInfo(itemTypeInfo, isNullable);
            }
            return CreateCollectionTypeInfo(CreateObjectTypeInfo(null, false), isNullable);
        }

        // Обрабатываем объекты
        if (IsObject(schema))
        {
            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                // Пытаемся найти имя схемы
                var objectSchemaName = schemaResolver.GetSchemaName(schema, string.Empty);
                
                if (!string.IsNullOrEmpty(objectSchemaName) && objectSchemaName != "object")
                {
                    var resolvedTypeName = NamingConverter.ToPascalCase(objectSchemaName);
                    return CreateObjectTypeInfo(resolvedTypeName, isNullable);
                }
                
                // Если имя не найдено, проверяем разрешенную схему
                var resolvedSchema = schemaResolver.ResolveSchema(schema);
                var resolvedSchemaName = schemaResolver.GetSchemaName(resolvedSchema, string.Empty);
                
                if (!string.IsNullOrEmpty(resolvedSchemaName) && resolvedSchemaName != "object")
                {
                    var resolvedTypeName = NamingConverter.ToPascalCase(resolvedSchemaName);
                    return CreateObjectTypeInfo(resolvedTypeName, isNullable);
                }
                
                return CreateObjectTypeInfo(null, isNullable);
            }

            if (IsDictionary(schema))
            {
                var valueTypeInfo = schema.AdditionalProperties != null
                    ? MapTypeInfoInternal(schema.AdditionalProperties, schemaResolver, false)
                    : CreateObjectTypeInfo(null, false);
                var keyTypeInfo = TypeInfo.Simple("string", isPrimitive: true, language: Language);
                return CreateDictionaryTypeInfo(keyTypeInfo, valueTypeInfo, isNullable);
            }

            return CreateObjectTypeInfo(null, isNullable);
        }

        // Обрабатываем примитивные типы
        if (IsPrimitive(schema) && schema.Type.HasValue)
        {
            var type = schema.Type.Value;
            var format = schema.Format?.ToLowerInvariant();
            var baseType = type & ~JsonSchemaType.Null;

            var (baseTypeName, isPrimitiveType, isFile) = (baseType, format) switch
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

            return CreatePrimitiveTypeInfo(baseTypeName, isPrimitiveType, isFile, isNullable);
        }

        // Если тип не определен, возвращаем object
        return CreateObjectTypeInfo(null, isNullable);
    }

    private string MapTypeInternal(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable)
    {
        // Проверяем на ссылку на схему
        var typeName = ResolveSchemaReference(schema, schemaResolver);
        if (!string.IsNullOrEmpty(typeName))
        {
            return isNullable ? $"{typeName}?" : typeName;
        }

        // Обрабатываем allOf (наследование)
        if (schema.AllOf != null && schema.AllOf.Count > 0)
        {
            string? baseTypeName = null;
            foreach (var subSchema in schema.AllOf)
            {
                var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                baseTypeName = ResolveSchemaReference(resolvedSubSchema, schemaResolver);
                if (!string.IsNullOrEmpty(baseTypeName))
                {
                    break;
                }
            }
            
            if (!string.IsNullOrEmpty(baseTypeName))
            {
                return isNullable ? $"{baseTypeName}?" : baseTypeName;
            }
            
            var lastSchema = schema.AllOf.Last();
            var resolvedLastSchema = schemaResolver.ResolveSchema(lastSchema);
            return MapTypeInternal(resolvedLastSchema, schemaResolver, isNullable);
        }

        // Обрабатываем oneOf/anyOf
        if ((schema.OneOf != null && schema.OneOf.Count > 0) || (schema.AnyOf != null && schema.AnyOf.Count > 0))
        {
            var schemas = schema.OneOf ?? schema.AnyOf ?? Array.Empty<IOpenApiSchema>();
            var commonBaseType = FindCommonBaseType(schemas, schemaResolver);
            if (!string.IsNullOrEmpty(commonBaseType))
            {
                return isNullable ? $"{commonBaseType}?" : commonBaseType;
            }
            return isNullable ? "object?" : "object";
        }

        // Обрабатываем массивы
        if (IsCollection(schema))
        {
            if (schema.Items != null)
            {
                var itemType = MapTypeInternal(schema.Items, schemaResolver, false);
                return isNullable ? $"List<{itemType}>?" : $"List<{itemType}>";
            }
            return isNullable ? "List<object>?" : "List<object>";
        }

        // Обрабатываем объекты
        if (IsObject(schema))
        {
            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                return isNullable ? "object?" : "object";
            }

            if (IsDictionary(schema))
            {
                var valueType = schema.AdditionalProperties != null
                    ? MapTypeInternal(schema.AdditionalProperties, schemaResolver, false)
                    : "object";
                return isNullable ? $"Dictionary<string, {valueType}>?" : $"Dictionary<string, {valueType}>";
            }

            return isNullable ? "object?" : "object";
        }

        // Обрабатываем примитивные типы
        if (IsPrimitive(schema) && schema.Type.HasValue)
        {
            var type = schema.Type.Value;
            var format = schema.Format?.ToLowerInvariant();
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

        return isNullable ? "object?" : "object";
    }

    /// <summary>
    /// Создает TypeInfo для коллекции
    /// </summary>
    protected override TypeInfo CreateCollectionTypeInfo(TypeInfo itemType, bool isNullable)
    {
        return TypeInfo.Collection("List", itemType, isNullable, Language);
    }

    /// <summary>
    /// Создает TypeInfo для словаря
    /// </summary>
    protected override TypeInfo CreateDictionaryTypeInfo(TypeInfo keyType, TypeInfo valueType, bool isNullable)
    {
        return TypeInfo.Dictionary("Dictionary", keyType, valueType, isNullable, Language);
    }

    /// <summary>
    /// Создает TypeInfo для примитивного типа
    /// </summary>
    protected override TypeInfo CreatePrimitiveTypeInfo(string baseTypeName, bool isPrimitive, bool isFile, bool isNullable)
    {
        var typeInfo = TypeInfo.Simple(baseTypeName, isPrimitive, isFile, Language);
        
        // Для string, byte[], object nullable не добавляется через "?"
        if (isNullable && baseTypeName != "string" && baseTypeName != "byte[]" && baseTypeName != "object")
        {
            return TypeInfo.Nullable(typeInfo);
        }
        
        return typeInfo;
    }

    /// <summary>
    /// Создает TypeInfo для объекта
    /// </summary>
    protected override TypeInfo CreateObjectTypeInfo(string? typeName, bool isNullable)
    {
        var typeInfo = TypeInfo.Simple(typeName ?? "object", language: Language);
        return isNullable ? TypeInfo.Nullable(typeInfo) : typeInfo;
    }

    /// <summary>
    /// Обрабатывает allOf (наследование)
    /// </summary>
    protected override TypeInfo HandleAllOf(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable)
    {
        if (schema.AllOf == null)
            return CreateObjectTypeInfo(null, isNullable);
        
        string? baseTypeName = null;
        foreach (var subSchema in schema.AllOf)
        {
            var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
            baseTypeName = ResolveSchemaReference(resolvedSubSchema, schemaResolver);
            if (!string.IsNullOrEmpty(baseTypeName))
            {
                break;
            }
        }
        
        if (!string.IsNullOrEmpty(baseTypeName))
        {
            return CreateReferenceTypeInfo(baseTypeName, isNullable);
        }
        
        var lastSchema = schema.AllOf.Last();
        var resolvedLastSchema = schemaResolver.ResolveSchema(lastSchema);
        return MapTypeInfoInternal(resolvedLastSchema, schemaResolver, isNullable);
    }

    /// <summary>
    /// Обрабатывает oneOf/anyOf (union)
    /// </summary>
    protected override TypeInfo HandleOneOfAnyOf(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable)
    {
        var schemas = schema.OneOf ?? schema.AnyOf ?? Array.Empty<IOpenApiSchema>();
        var commonBaseType = FindCommonBaseType(schemas, schemaResolver);
        if (!string.IsNullOrEmpty(commonBaseType))
        {
            return CreateReferenceTypeInfo(commonBaseType, isNullable);
        }
        
        return CreateObjectTypeInfo(null, isNullable);
    }
}

