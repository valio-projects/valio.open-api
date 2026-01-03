using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;

namespace ValIO.Core.Services;

/// <summary>
/// Построитель TypeInfo из OpenAPI схем
/// </summary>
public class TypeInfoBuilder
{
    private readonly ITypeMapper _typeMapper;
    private readonly ISchemaResolver _schemaResolver;
    private readonly GenerationLanguage _language;

    public TypeInfoBuilder(ITypeMapper typeMapper, ISchemaResolver schemaResolver, GenerationLanguage language)
    {
        _typeMapper = typeMapper;
        _schemaResolver = schemaResolver;
        _language = language;
    }

    /// <summary>
    /// Строит TypeInfo из OpenAPI схемы
    /// </summary>
    public TypeInfo BuildFromSchema(IOpenApiSchema schema, bool isNullable = false)
    {
        if (schema == null)
        {
            return CreateDefaultTypeInfo(isNullable);
        }

        return _typeMapper.MapPropertyTypeInfo(schema, !isNullable, _schemaResolver);
    }

    /// <summary>
    /// Строит TypeInfo для коллекции
    /// </summary>
    public TypeInfo BuildCollection(IOpenApiSchema itemSchema, bool isNullable = false)
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
    public TypeInfo BuildDictionary(IOpenApiSchema valueSchema, IOpenApiSchema? keySchema = null, bool isNullable = false)
    {
        var keyTypeInfo = keySchema != null 
            ? BuildFromSchema(keySchema, false) 
            : TypeInfo.Simple("string", isPrimitive: true, language: _language);

        var valueTypeInfo = valueSchema != null
            ? BuildFromSchema(valueSchema, false)
            : CreateDefaultTypeInfo(false);

        return CreateDictionaryTypeInfo(keyTypeInfo, valueTypeInfo, isNullable);
    }

    /// <summary>
    /// Строит nullable TypeInfo
    /// </summary>
    public TypeInfo BuildNullable(TypeInfo innerType)
    {
        return TypeInfo.Nullable(innerType);
    }

    /// <summary>
    /// Создает TypeInfo для коллекции
    /// </summary>
    private TypeInfo CreateCollectionTypeInfo(TypeInfo itemType, bool isNullable)
    {
        return _language switch
        {
            GenerationLanguage.CSharp => TypeInfo.Collection("List", itemType, isNullable, _language),
            GenerationLanguage.TypeScript => TypeInfo.Collection("Array", itemType, isNullable, _language),
            _ => TypeInfo.Collection("List", itemType, isNullable, _language)
        };
    }

    /// <summary>
    /// Создает TypeInfo для словаря
    /// </summary>
    private TypeInfo CreateDictionaryTypeInfo(TypeInfo keyType, TypeInfo valueType, bool isNullable)
    {
        return _language switch
        {
            GenerationLanguage.CSharp => TypeInfo.Dictionary("Dictionary", keyType, valueType, isNullable, _language),
            GenerationLanguage.TypeScript => TypeInfo.Dictionary("Record", keyType, valueType, isNullable, _language),
            _ => TypeInfo.Dictionary("Dictionary", keyType, valueType, isNullable, _language)
        };
    }

    /// <summary>
    /// Создает TypeInfo по умолчанию
    /// </summary>
    private TypeInfo CreateDefaultTypeInfo(bool isNullable)
    {
        var defaultType = _language switch
        {
            GenerationLanguage.CSharp => "object",
            GenerationLanguage.TypeScript => "any",
            _ => "object"
        };

        var typeInfo = TypeInfo.Simple(defaultType, language: _language);
        return isNullable ? TypeInfo.Nullable(typeInfo) : typeInfo;
    }
}
