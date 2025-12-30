using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using ValIO.Core.Interfaces;

namespace ValIO.Core.Services;

/// <summary>
/// Реализация разрешителя схем OpenAPI
/// </summary>
public class SchemaResolver : ISchemaResolver
{
    private readonly OpenApiDocument _document;

    public SchemaResolver(OpenApiDocument document)
    {
        _document = document;
    }

    public OpenApiSchema ResolveReference(OpenApiReference reference)
    {
        if (reference == null || string.IsNullOrEmpty(reference.Id))
            throw new ArgumentException("Reference is null or empty", nameof(reference));

        // Разрешаем ссылку на схему в компонентах
        if (reference.Type == ReferenceType.Schema && _document.Components?.Schemas != null)
        {
            var schemaName = reference.Id;
            if (_document.Components.Schemas.TryGetValue(schemaName, out var schema))
            {
                return schema;
            }
        }

        throw new InvalidOperationException($"Cannot resolve reference: {reference.Id}");
    }

    public OpenApiSchema ResolveSchema(OpenApiSchema schema)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        // Если схема содержит ссылку, разрешаем ее
        if (schema.Reference != null)
        {
            return ResolveReference(schema.Reference);
        }

        return schema;
    }

    public string GetSchemaName(OpenApiSchema schema, string defaultName)
    {
        if (schema == null)
            return defaultName;

        // Если есть ссылка, используем имя из ссылки
        if (schema.Reference != null && !string.IsNullOrEmpty(schema.Reference.Id))
        {
            return schema.Reference.Id;
        }

        // Если есть заголовок в расширениях
        if (schema.Extensions != null && schema.Extensions.TryGetValue("x-name", out var nameExtension))
        {
            return nameExtension?.ToString() ?? defaultName;
        }

        return defaultName;
    }
}

