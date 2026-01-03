using ValIO.Core.Models;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для маппинга типов OpenAPI на типы целевого языка
/// </summary>
public interface ITypeMapper
{
    /// <summary>
    /// Маппинг типа схемы на тип целевого языка
    /// </summary>
    string MapType(IOpenApiSchema schema, ISchemaResolver schemaResolver);
    
    /// <summary>
    /// Маппинг типа свойства с учетом обязательности
    /// </summary>
    string MapPropertyType(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver);
    
    /// <summary>
    /// Маппинг типа enum
    /// </summary>
    string MapEnumType(IOpenApiSchema schema, ISchemaResolver schemaResolver);
    
    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo с мета-данными
    /// </summary>
    TypeInfo MapTypeInfo(IOpenApiSchema schema, ISchemaResolver schemaResolver);
    
    /// <summary>
    /// Маппит схему OpenAPI в TypeInfo для свойства с учетом nullable
    /// </summary>
    TypeInfo MapPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver);
}

