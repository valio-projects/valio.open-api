
namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для маппинга типов OpenAPI на типы целевого языка
/// </summary>
public interface ITypeMapper
{
    string MapType(OpenApiSchema schema);
    string MapPropertyType(OpenApiSchema schema, bool isRequired);
    string MapEnumType(OpenApiSchema schema);
}

