using System.Text.Json;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;

namespace ValIO.Core.Services;

/// <summary>
/// Базовый класс для маппинга типов OpenAPI на типы целевого языка
/// </summary>
public abstract class BaseTypeMapper : ITypeMapper
{
    protected readonly INamingConverter NamingConverter;
    protected readonly GenerationLanguage Language;

    protected BaseTypeMapper(INamingConverter namingConverter, GenerationLanguage language)
    {
        NamingConverter = namingConverter;
        Language = language;
    }

    /// <summary>
    /// Маппинг типа схемы на тип целевого языка
    /// </summary>
    public abstract string MapType(IOpenApiSchema schema, ISchemaResolver schemaResolver);

    /// <summary>
    /// Маппинг типа свойства с учетом обязательности
    /// </summary>
    public abstract string MapPropertyType(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver);

    /// <summary>
    /// Маппинг типа enum
    /// </summary>
    public abstract string MapEnumType(IOpenApiSchema schema, ISchemaResolver schemaResolver);

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo с мета-данными
    /// </summary>
    public abstract TypeInfo MapTypeInfo(IOpenApiSchema schema, ISchemaResolver schemaResolver);

    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo для свойства с учетом nullable
    /// </summary>
    public abstract TypeInfo MapPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver);

    /// <summary>
    /// Проверяет, является ли схема nullable
    /// </summary>
    protected static bool IsSchemaNullable(IOpenApiSchema schema)
    {
        if (schema == null)
            return false;

        // Проверяем, содержит ли тип Type.Null
        if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Null))
            return true;

        // Проверяем расширения для обратной совместимости с OpenAPI 3.0
        if (schema.Extensions != null && schema.Extensions.TryGetValue(OpenApiConstants.NullableExtension, out var nullableExtension))
        {
            if (nullableExtension is JsonNodeExtension { Node: { } jsonNode } && jsonNode.GetValueKind() == JsonValueKind.True)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Разрешает ссылку на схему и возвращает имя типа
    /// </summary>
    protected string? ResolveSchemaReference(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> referenceHolder)
        {
            var reference = referenceHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                return NamingConverter.ToPascalCase(reference.Id);
            }
        }

        // Также проверяем через GetSchemaName для случаев, когда ссылка может быть в другом формате
        var schemaName = schemaResolver.GetSchemaName(schema, string.Empty);
        if (!string.IsNullOrEmpty(schemaName) && schemaName != "object")
        {
            return NamingConverter.ToPascalCase(schemaName);
        }

        return null;
    }

    /// <summary>
    /// Определяет, является ли схема коллекцией (массивом)
    /// </summary>
    protected static bool IsCollection(IOpenApiSchema schema)
    {
        return schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Array);
    }

    /// <summary>
    /// Определяет, является ли схема словарем (объектом с AdditionalProperties)
    /// </summary>
    protected static bool IsDictionary(IOpenApiSchema schema)
    {
        return schema.AdditionalProperties != null || schema.AdditionalPropertiesAllowed;
    }

    /// <summary>
    /// Определяет, является ли схема примитивным типом
    /// </summary>
    protected static bool IsPrimitive(IOpenApiSchema schema)
    {
        if (!schema.Type.HasValue)
            return false;

        var type = schema.Type.Value & ~JsonSchemaType.Null;
        return type == JsonSchemaType.Boolean ||
               type == JsonSchemaType.Integer ||
               type == JsonSchemaType.Number ||
               type == JsonSchemaType.String;
    }

    /// <summary>
    /// Определяет, является ли схема объектом
    /// </summary>
    protected static bool IsObject(IOpenApiSchema schema)
    {
        return schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Object);
    }

    /// <summary>
    /// Определяет, является ли схема файлом
    /// </summary>
    protected static bool IsFile(IOpenApiSchema schema)
    {
        var format = schema.Format?.ToLowerInvariant();
        return format == "binary" || format == "byte";
    }

    /// <summary>
    /// Находит общий базовый тип для oneOf/anyOf (для упрощения)
    /// </summary>
    protected string? FindCommonBaseType(IEnumerable<IOpenApiSchema> schemas, ISchemaResolver schemaResolver)
    {
        var resolvedSchemas = schemas.Select(s => schemaResolver.ResolveSchema(s)).ToList();
        
        // Если все схемы ссылаются на один и тот же тип, возвращаем его
        var typeNames = new HashSet<string>();
        foreach (var schema in resolvedSchemas)
        {
            var typeName = ResolveSchemaReference(schema, schemaResolver);
            if (!string.IsNullOrEmpty(typeName))
            {
                typeNames.Add(typeName);
            }
        }

        if (typeNames.Count == 1)
        {
            return typeNames.First();
        }

        // Если не нашли общий тип, проверяем, есть ли общий базовый тип через allOf
        foreach (var schema in resolvedSchemas)
        {
            if (schema.AllOf != null && schema.AllOf.Count > 0)
            {
                foreach (var subSchema in schema.AllOf)
                {
                    var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
                    var baseTypeName = ResolveSchemaReference(resolvedSubSchema, schemaResolver);
                    if (!string.IsNullOrEmpty(baseTypeName))
                    {
                        typeNames.Add(baseTypeName);
                    }
                }
            }
        }

        if (typeNames.Count == 1)
        {
            return typeNames.First();
        }

        return null;
    }

    /// <summary>
    /// Создает TypeInfo для ссылки на схему
    /// </summary>
    protected TypeInfo CreateReferenceTypeInfo(string typeName, bool isNullable)
    {
        var typeInfo = TypeInfo.Simple(typeName, language: Language);
        return isNullable ? TypeInfo.Nullable(typeInfo) : typeInfo;
    }

    /// <summary>
    /// Создает TypeInfo для коллекции
    /// </summary>
    protected abstract TypeInfo CreateCollectionTypeInfo(TypeInfo itemType, bool isNullable);

    /// <summary>
    /// Создает TypeInfo для словаря
    /// </summary>
    protected abstract TypeInfo CreateDictionaryTypeInfo(TypeInfo keyType, TypeInfo valueType, bool isNullable);

    /// <summary>
    /// Создает TypeInfo для примитивного типа
    /// </summary>
    protected abstract TypeInfo CreatePrimitiveTypeInfo(string baseTypeName, bool isPrimitive, bool isFile, bool isNullable);

    /// <summary>
    /// Создает TypeInfo для объекта
    /// </summary>
    protected abstract TypeInfo CreateObjectTypeInfo(string? typeName, bool isNullable);

    /// <summary>
    /// Обрабатывает allOf (наследование/intersection)
    /// </summary>
    protected abstract TypeInfo HandleAllOf(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable);

    /// <summary>
    /// Обрабатывает oneOf/anyOf (union)
    /// </summary>
    protected abstract TypeInfo HandleOneOfAnyOf(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable);
}
