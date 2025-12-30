using Microsoft.OpenApi;
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

    public IOpenApiSchema ResolveReference(BaseOpenApiReference reference)
    {
        if (reference == null || string.IsNullOrEmpty(reference.Id))
            throw new ArgumentException("Reference is null or empty", nameof(reference));

        // Разрешаем ссылку на схему в компонентах
        if (reference.Type == ReferenceType.Schema && _document.Components?.Schemas != null)
        {
            var schemaName = reference.Id;
            if (_document.Components.Schemas.TryGetValue(schemaName, out var schema))
            {
                // Если схема является ссылкой, получаем целевую схему
                if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> referenceHolder)
                {
                    return referenceHolder.Target ?? schema;
                }
                return schema;
            }
        }

        throw new InvalidOperationException($"Cannot resolve reference: {reference.Id}");
    }

    public IOpenApiSchema ResolveSchema(IOpenApiSchema schema)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        // Если схема является ссылкой, получаем целевую схему
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> referenceHolder)
        {
            var target = referenceHolder.Target;
            if (target != null)
            {
                // Рекурсивно разрешаем, если целевая схема тоже ссылка
                return ResolveSchema(target);
            }
            
            // Если Target не разрешен, пытаемся разрешить через документ
            var reference = referenceHolder.Reference;
            if (reference != null)
            {
                return ResolveReference(reference);
            }
        }

        return schema;
    }

    public string GetSchemaName(IOpenApiSchema schema, string defaultName)
    {
        if (schema == null)
            return defaultName;

        // Если схема является ссылкой, используем имя из ссылки
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> referenceHolder)
        {
            var reference = referenceHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                return reference.Id;
            }
        }

        // Если есть заголовок в расширениях
        if (schema.Extensions != null && schema.Extensions.TryGetValue("x-name", out var nameExtension))
        {
            return nameExtension?.ToString() ?? defaultName;
        }

        return defaultName;
    }
}

