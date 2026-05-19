using Microsoft.OpenApi;
using ValIO.Core.Interfaces;

namespace ValIO.Core.Services;

/// <summary>
/// Proxy поверх ISchemaResolver с кэшированием по reference ID.
/// Устраняет повторное разрешение одних и тех же $ref в больших спецификациях.
/// </summary>
public sealed class CachingSchemaResolver : ISchemaResolver
{
    private readonly ISchemaResolver _inner;
    private readonly Dictionary<string, IOpenApiSchema> _cache = new(StringComparer.Ordinal);

    public CachingSchemaResolver(ISchemaResolver inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public IOpenApiSchema ResolveReference(BaseOpenApiReference reference)
    {
        var key = reference?.Id ?? string.Empty;
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var resolved = _inner.ResolveReference(reference!);
        _cache[key] = resolved;
        return resolved;
    }

    public IOpenApiSchema ResolveSchema(IOpenApiSchema schema)
        => _inner.ResolveSchema(schema);

    public string GetSchemaName(IOpenApiSchema schema, string defaultName)
        => _inner.GetSchemaName(schema, defaultName);
}
