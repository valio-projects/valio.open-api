using System.Text;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;

namespace ValIO.Generators.TypeScript;

/// <summary>
/// Маппер типов OpenAPI на типы TypeScript
/// </summary>
public class TypeScriptTypeMapper : BaseTypeMapper
{
    public TypeScriptTypeMapper(INamingConverter namingConverter)
        : base(namingConverter, GenerationLanguage.TypeScript)
    {
    }

    public override string MapType(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return "any";

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        return MapTypeInternal(resolvedSchema, schemaResolver, false);
    }

    public override string MapPropertyType(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return "any";

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

    private string MapTypeInternal(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable)
    {
        // Проверяем на ссылку на схему
        var typeName = ResolveSchemaReference(schema, schemaResolver);
        if (!string.IsNullOrEmpty(typeName))
        {
            return typeName; // Для TypeScript nullable обрабатывается через optional properties
        }

        // Обрабатываем allOf (intersection type в TypeScript)
        if (schema.AllOf != null && schema.AllOf.Count > 0)
        {
            var types = new List<string>();
            foreach (var subSchema in schema.AllOf)
            {
                var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                types.Add(MapTypeInternal(resolvedSubSchema, schemaResolver, false));
            }
            return string.Join(" & ", types);
        }

        // Обрабатываем oneOf/anyOf (union type)
        if ((schema.OneOf != null && schema.OneOf.Count > 0) || (schema.AnyOf != null && schema.AnyOf.Count > 0))
        {
            var schemas = schema.OneOf ?? schema.AnyOf ?? Array.Empty<IOpenApiSchema>();
            var types = new List<string>();
            foreach (var subSchema in schemas)
            {
                var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                types.Add(MapTypeInternal(resolvedSubSchema, schemaResolver, false));
            }
            return string.Join(" | ", types);
        }

        // Обрабатываем массивы
        if (IsCollection(schema))
        {
            if (schema.Items != null)
            {
                var itemType = MapTypeInternal(schema.Items, schemaResolver, false);
                return $"{itemType}[]";
            }
            return "any[]";
        }

        // Обрабатываем объекты
        if (IsObject(schema))
        {
            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                return "Record<string, any>";
            }

            if (IsDictionary(schema))
            {
                var valueType = schema.AdditionalProperties != null
                    ? MapTypeInternal(schema.AdditionalProperties, schemaResolver, false)
                    : "any";
                return $"Record<string, {valueType}>";
            }

            return "Record<string, any>";
        }

        // Обрабатываем примитивные типы
        if (IsPrimitive(schema) && schema.Type.HasValue)
        {
            var type = schema.Type.Value;
            var format = schema.Format?.ToLowerInvariant();
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

            return tsType;
        }

        return "any";
    }

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo с мета-данными для TypeScript
    /// </summary>
    public override TypeInfo MapTypeInfo(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return TypeInfo.Simple("any", isPrimitive: true, language: Language);

        var resolvedSchema = schemaResolver.ResolveSchema(schema);
        return MapTypeInfoInternal(resolvedSchema, schemaResolver, false);
    }

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo для свойства с учетом nullable
    /// </summary>
    public override TypeInfo MapPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        if (schema == null)
            return TypeInfo.Simple("any", isPrimitive: true, language: Language);

        // Проверяем на ссылку ДО разрешения, чтобы сохранить имя схемы
        var typeName = ResolveSchemaReference(schema, schemaResolver);
        if (!string.IsNullOrEmpty(typeName))
        {
            var typeInfo = TypeInfo.Simple(typeName, language: Language);
            // Разрешаем схему только для проверки nullable
            var resolvedSchemaForNullable = schemaResolver.ResolveSchema(schema);
            var isNullableValue = !isRequired || IsSchemaNullable(resolvedSchemaForNullable);
            // Для TypeScript не добавляем ? к типу, только устанавливаем IsNullable и IsOptional
            if (isNullableValue)
            {
                typeInfo.IsNullable = true;
                typeInfo.IsOptional = true;
            }
            return typeInfo;
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
        // Проверяем на ссылку на схему
        var typeName = ResolveSchemaReference(schema, schemaResolver);
        if (!string.IsNullOrEmpty(typeName))
        {
            var typeInfo = TypeInfo.Simple(typeName, language: Language);
            typeInfo.IsNullable = isNullable;
            typeInfo.IsOptional = isNullable;
            return typeInfo;
        }

        // Обрабатываем allOf (intersection type в TypeScript)
        if (schema.AllOf != null && schema.AllOf.Count > 0)
        {
            return HandleAllOf(schema, schemaResolver, isNullable);
        }

        // Обрабатываем oneOf/anyOf (union type в TypeScript)
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
                var arrayType = CreateCollectionTypeInfo(itemTypeInfo, isNullable);
                // Переопределяем FullTypeName для TypeScript формата
                arrayType.FullTypeName = $"{itemTypeInfo.FullTypeName}[]";
                arrayType.IsOptional = isNullable;
                return arrayType;
            }
            var anyArrayType = CreateCollectionTypeInfo(TypeInfo.Simple("any", isPrimitive: true, language: Language), isNullable);
            anyArrayType.FullTypeName = "any[]";
            anyArrayType.IsOptional = isNullable;
            return anyArrayType;
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
                    var objectTypeInfo = CreateObjectTypeInfo(resolvedTypeName, isNullable);
                    objectTypeInfo.IsOptional = isNullable;
                    return objectTypeInfo;
                }
                
                // Если имя не найдено, это inline объект с Properties - создаем словарь Record<string, any>
                var inlineValueType = TypeInfo.Simple("any", isPrimitive: true, language: Language);
                var inlineKeyType = TypeInfo.Simple("string", isPrimitive: true, language: Language);
                var inlineDictType = CreateDictionaryTypeInfo(inlineKeyType, inlineValueType, isNullable);
                inlineDictType.IsOptional = isNullable;
                return inlineDictType;
            }

            if (IsDictionary(schema))
            {
                var dictValueType = schema.AdditionalProperties != null
                    ? MapTypeInfoInternal(schema.AdditionalProperties, schemaResolver, false)
                    : TypeInfo.Simple("any", isPrimitive: true, language: Language);
                var dictKeyType = TypeInfo.Simple("string", isPrimitive: true, language: Language);
                var dictType = CreateDictionaryTypeInfo(dictKeyType, dictValueType, isNullable);
                dictType.IsOptional = isNullable;
                return dictType;
            }

            // Для объекта без Properties создаем словарь Record<string, any>
            var defaultValueType = TypeInfo.Simple("any", isPrimitive: true, language: Language);
            var defaultKeyType = TypeInfo.Simple("string", isPrimitive: true, language: Language);
            var defaultObjectType = CreateDictionaryTypeInfo(defaultKeyType, defaultValueType, isNullable);
            defaultObjectType.IsOptional = isNullable;
            return defaultObjectType;
        }

        // Обрабатываем примитивные типы
        if (IsPrimitive(schema) && schema.Type.HasValue)
        {
            var type = schema.Type.Value;
            var format = schema.Format?.ToLowerInvariant();
            var baseType = type & ~JsonSchemaType.Null;

            var (baseTypeName, isPrimitiveType, isFile) = (baseType, format) switch
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

            var typeInfo = CreatePrimitiveTypeInfo(baseTypeName, isPrimitiveType, isFile, isNullable);
            typeInfo.IsOptional = isNullable;
            return typeInfo;
        }

        // Если тип не определен, возвращаем any
        var defaultTypeInfo = TypeInfo.Simple("any", isPrimitive: true, language: Language);
        defaultTypeInfo.IsNullable = isNullable;
        defaultTypeInfo.IsOptional = isNullable;
        return defaultTypeInfo;
    }

    /// <summary>
    /// Создает TypeInfo для коллекции
    /// </summary>
    protected override TypeInfo CreateCollectionTypeInfo(TypeInfo itemType, bool isNullable)
    {
        return TypeInfo.Collection("Array", itemType, isNullable, Language);
    }

    /// <summary>
    /// Создает TypeInfo для словаря
    /// </summary>
    protected override TypeInfo CreateDictionaryTypeInfo(TypeInfo keyType, TypeInfo valueType, bool isNullable)
    {
        return TypeInfo.Dictionary("Record", keyType, valueType, isNullable, Language);
    }

    /// <summary>
    /// Создает TypeInfo для примитивного типа
    /// </summary>
    protected override TypeInfo CreatePrimitiveTypeInfo(string baseTypeName, bool isPrimitive, bool isFile, bool isNullable)
    {
        var typeInfo = TypeInfo.Simple(baseTypeName, isPrimitive, isFile, Language);
        typeInfo.IsNullable = isNullable;
        return typeInfo;
    }

    /// <summary>
    /// Создает TypeInfo для объекта
    /// </summary>
    protected override TypeInfo CreateObjectTypeInfo(string? typeName, bool isNullable)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            // Для объекта без имени создаем словарь Record<string, any>
            var valueTypeInfo = TypeInfo.Simple("any", isPrimitive: true, language: Language);
            var keyTypeInfo = TypeInfo.Simple("string", isPrimitive: true, language: Language);
            return CreateDictionaryTypeInfo(keyTypeInfo, valueTypeInfo, isNullable);
        }
        
        var typeInfo = TypeInfo.Simple(typeName, language: Language);
        typeInfo.IsNullable = isNullable;
        return typeInfo;
    }

    /// <summary>
    /// Обрабатывает allOf (intersection type в TypeScript)
    /// </summary>
    protected override TypeInfo HandleAllOf(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable)
    {
        if (schema.AllOf == null)
            return CreateObjectTypeInfo(null, isNullable);
        
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
            types[0].IsNullable = isNullable;
            types[0].IsOptional = isNullable;
            return types[0];
        }
        return CreateObjectTypeInfo(null, isNullable);
    }

    /// <summary>
    /// Обрабатывает oneOf/anyOf (union type в TypeScript)
    /// </summary>
    protected override TypeInfo HandleOneOfAnyOf(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable)
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
            types[0].IsNullable = isNullable;
            types[0].IsOptional = isNullable;
            return types[0];
        }
        return CreateObjectTypeInfo(null, isNullable);
    }
}

