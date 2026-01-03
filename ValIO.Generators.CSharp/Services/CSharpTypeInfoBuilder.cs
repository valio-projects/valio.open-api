using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;

namespace ValIO.Generators.CSharp.Services;

/// <summary>
/// Построитель TypeInfo для C#
/// </summary>
public class CSharpTypeInfoBuilder : BaseTypeInfoBuilder
{
    public CSharpTypeInfoBuilder(ITypeMapper typeMapper, ISchemaResolver schemaResolver)
        : base(typeMapper, schemaResolver, GenerationLanguage.CSharp)
    {
    }

    /// <summary>
    /// Создает TypeInfo для коллекции C#
    /// </summary>
    protected override TypeInfo CreateCollectionTypeInfo(TypeInfo itemType, bool isNullable)
    {
        return TypeInfo.Collection("List", itemType, isNullable, Language);
    }

    /// <summary>
    /// Создает TypeInfo для словаря C#
    /// </summary>
    protected override TypeInfo CreateDictionaryTypeInfo(TypeInfo keyType, TypeInfo valueType, bool isNullable)
    {
        return TypeInfo.Dictionary("Dictionary", keyType, valueType, isNullable, Language);
    }

    /// <summary>
    /// Создает TypeInfo по умолчанию для C#
    /// </summary>
    protected override TypeInfo CreateDefaultTypeInfo(bool isNullable)
    {
        var typeInfo = TypeInfo.Simple("object", language: Language);
        return isNullable ? TypeInfo.Nullable(typeInfo) : typeInfo;
    }
}
