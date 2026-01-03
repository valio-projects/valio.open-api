using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;

namespace ValIO.Core.Services;

/// <summary>
/// Базовый класс для построения TypeInfo из OpenAPI схем
/// </summary>
public abstract class BaseTypeInfoBuilder : ITypeInfoBuilder
{
    protected readonly ITypeMapper TypeMapper;
    protected readonly ISchemaResolver SchemaResolver;
    
    /// <summary>
    /// Язык программирования, для которого строится TypeInfo
    /// </summary>
    public GenerationLanguage Language { get; }

    protected BaseTypeInfoBuilder(ITypeMapper typeMapper, ISchemaResolver schemaResolver, GenerationLanguage language)
    {
        TypeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        SchemaResolver = schemaResolver ?? throw new ArgumentNullException(nameof(schemaResolver));
        Language = language;
    }

    /// <summary>
    /// Строит TypeInfo из OpenAPI схемы
    /// </summary>
    public virtual TypeInfo BuildFromSchema(IOpenApiSchema schema, bool isNullable = false)
    {
        if (schema == null)
        {
            return CreateDefaultTypeInfo(isNullable);
        }

        return TypeMapper.MapPropertyTypeInfo(schema, !isNullable, SchemaResolver);
    }

    /// <summary>
    /// Строит TypeInfo для коллекции
    /// </summary>
    public virtual TypeInfo BuildCollection(IOpenApiSchema itemSchema, bool isNullable = false)
    {
        if (itemSchema == null)
        {
            var defaultItemType = CreateDefaultTypeInfo(false);
            return CreateCollectionTypeInfo(defaultItemType, isNullable);
        }

        var itemTypeInfo = BuildFromSchema(itemSchema, false);
        return CreateCollectionTypeInfo(itemTypeInfo, isNullable);
    }

    /// <summary>
    /// Строит TypeInfo для словаря
    /// </summary>
    public virtual TypeInfo BuildDictionary(IOpenApiSchema valueSchema, IOpenApiSchema? keySchema = null, bool isNullable = false)
    {
        var keyTypeInfo = keySchema != null 
            ? BuildFromSchema(keySchema, false) 
            : TypeInfo.Simple("string", isPrimitive: true, language: Language);

        var valueTypeInfo = valueSchema != null
            ? BuildFromSchema(valueSchema, false)
            : CreateDefaultTypeInfo(false);

        return CreateDictionaryTypeInfo(keyTypeInfo, valueTypeInfo, isNullable);
    }

    /// <summary>
    /// Строит nullable TypeInfo
    /// </summary>
    public virtual TypeInfo BuildNullable(TypeInfo innerType)
    {
        return TypeInfo.Nullable(innerType);
    }

    /// <summary>
    /// Создает TypeInfo для коллекции (язык-специфичная реализация)
    /// </summary>
    protected abstract TypeInfo CreateCollectionTypeInfo(TypeInfo itemType, bool isNullable);

    /// <summary>
    /// Создает TypeInfo для словаря (язык-специфичная реализация)
    /// </summary>
    protected abstract TypeInfo CreateDictionaryTypeInfo(TypeInfo keyType, TypeInfo valueType, bool isNullable);

    /// <summary>
    /// Создает TypeInfo по умолчанию (язык-специфичная реализация)
    /// </summary>
    protected abstract TypeInfo CreateDefaultTypeInfo(bool isNullable);
}
