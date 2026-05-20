
using Microsoft.OpenApi;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для разрешения ссылок в OpenAPI схеме
/// </summary>
public interface ISchemaResolver
{
    IOpenApiSchema ResolveReference(BaseOpenApiReference reference);
    IOpenApiSchema ResolveSchema(IOpenApiSchema schema);
    string GetSchemaName(IOpenApiSchema schema, string defaultName);
}

