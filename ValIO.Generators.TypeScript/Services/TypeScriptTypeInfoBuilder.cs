using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;

namespace ValIO.Generators.TypeScript.Services;

/// <summary>
/// Построитель TypeInfo для TypeScript
/// </summary>
public class TypeScriptTypeInfoBuilder : BaseTypeInfoBuilder
{
    public TypeScriptTypeInfoBuilder(ITypeMapper typeMapper, ISchemaResolver schemaResolver)
        : base(typeMapper, schemaResolver, GenerationLanguage.TypeScript)
    {
    }

    /// <summary>
    /// Создает TypeInfo для коллекции TypeScript
    /// </summary>
    protected override TypeInfo CreateCollectionTypeInfo(TypeInfo itemType, bool isNullable)
    {
        return TypeInfo.Collection("Array", itemType, isNullable, Language);
    }

    /// <summary>
    /// Создает TypeInfo для словаря TypeScript
    /// </summary>
    protected override TypeInfo CreateDictionaryTypeInfo(TypeInfo keyType, TypeInfo valueType, bool isNullable)
    {
        return TypeInfo.Dictionary("Record", keyType, valueType, isNullable, Language);
    }

    /// <summary>
    /// Создает TypeInfo по умолчанию для TypeScript
    /// </summary>
    protected override TypeInfo CreateDefaultTypeInfo(bool isNullable)
    {
        var typeInfo = TypeInfo.Simple("any", language: Language);
        return isNullable ? TypeInfo.Nullable(typeInfo) : typeInfo;
    }
}
