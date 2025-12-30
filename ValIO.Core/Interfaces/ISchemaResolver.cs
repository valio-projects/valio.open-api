using Microsoft.OpenApi.Models;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для разрешения ссылок в OpenAPI схеме
/// </summary>
public interface ISchemaResolver
{
    OpenApiSchema ResolveReference(OpenApiReference reference);
    OpenApiSchema ResolveSchema(OpenApiSchema schema);
    string GetSchemaName(OpenApiSchema schema, string defaultName);
}

