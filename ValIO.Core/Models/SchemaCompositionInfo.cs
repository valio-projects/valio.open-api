using Microsoft.OpenApi;

namespace ValIO.Core.Models;

/// <summary>
/// Информация о композиции схем (allOf, oneOf, anyOf)
/// </summary>
public class SchemaCompositionInfo
{
    /// <summary>
    /// Тип композиции
    /// </summary>
    public SchemaCompositionType CompositionType { get; set; }
    
    /// <summary>
    /// Оригинальные под-схемы (до разрешения) - сохраняем для генераторов, которым нужны оригинальные ссылки
    /// </summary>
    public IReadOnlyList<IOpenApiSchema> OriginalSubSchemas { get; set; } = Array.Empty<IOpenApiSchema>();
    
    /// <summary>
    /// Разрешенные под-схемы
    /// </summary>
    public IReadOnlyList<IOpenApiSchema> ResolvedSubSchemas { get; set; } = Array.Empty<IOpenApiSchema>();
    
    /// <summary>
    /// Имена схем компонентов, на которые ссылаются под-схемы (если есть)
    /// </summary>
    public IReadOnlyList<string> ReferencedSchemaNames { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Информация о каждой под-схеме
    /// </summary>
    public IReadOnlyList<SubSchemaInfo> SubSchemaInfos { get; set; } = Array.Empty<SubSchemaInfo>();
}

/// <summary>
/// Тип композиции схем
/// </summary>
public enum SchemaCompositionType
{
    /// <summary>
    /// Нет композиции
    /// </summary>
    None,
    
    /// <summary>
    /// allOf - объединение всех свойств (наследование/intersection)
    /// </summary>
    AllOf,
    
    /// <summary>
    /// oneOf - один из вариантов (discriminated union)
    /// </summary>
    OneOf,
    
    /// <summary>
    /// anyOf - любой из вариантов (union)
    /// </summary>
    AnyOf
}

/// <summary>
/// Информация о под-схеме в композиции
/// </summary>
public class SubSchemaInfo
{
    /// <summary>
    /// Оригинальная схема (до разрешения)
    /// </summary>
    public IOpenApiSchema OriginalSchema { get; set; } = null!;
    
    /// <summary>
    /// Разрешенная схема
    /// </summary>
    public IOpenApiSchema ResolvedSchema { get; set; } = null!;
    
    /// <summary>
    /// Имя схемы компонента, на которую ссылается эта под-схема (если есть)
    /// </summary>
    public string? ReferencedSchemaName { get; set; }
    
    /// <summary>
    /// Является ли под-схема ссылкой на компонент
    /// </summary>
    public bool IsComponentReference { get; set; }
    
    /// <summary>
    /// Является ли под-схема inline схемой (объект без $ref)
    /// </summary>
    public bool IsInlineSchema { get; set; }
    
    /// <summary>
    /// Оригинальные Properties под-схемы (до разрешения) - для генераторов, которым нужны оригинальные ссылки
    /// </summary>
    public IDictionary<string, IOpenApiSchema>? OriginalProperties { get; set; }
}
