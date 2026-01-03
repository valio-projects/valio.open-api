using Microsoft.OpenApi;
using ValIO.Core.Models;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для построения TypeInfo из OpenAPI схем
/// </summary>
public interface ITypeInfoBuilder
{
    /// <summary>
    /// Язык программирования, для которого строится TypeInfo
    /// </summary>
    GenerationLanguage Language { get; }

    /// <summary>
    /// Строит TypeInfo из OpenAPI схемы
    /// </summary>
    TypeInfo BuildFromSchema(IOpenApiSchema schema, bool isNullable = false);

    /// <summary>
    /// Строит TypeInfo для коллекции
    /// </summary>
    TypeInfo BuildCollection(IOpenApiSchema itemSchema, bool isNullable = false);

    /// <summary>
    /// Строит TypeInfo для словаря
    /// </summary>
    TypeInfo BuildDictionary(IOpenApiSchema valueSchema, IOpenApiSchema? keySchema = null, bool isNullable = false);

    /// <summary>
    /// Строит nullable TypeInfo
    /// </summary>
    TypeInfo BuildNullable(TypeInfo innerType);
}
