using Microsoft.Extensions.Logging;
using ValIO.Parsers.Interfaces;

namespace ValIO.Desktop.Services;

/// <summary>Reads OpenAPI metadata for the desktop schema preview.</summary>
public sealed class SchemaPreviewer : ISchemaPreviewer
{
    /// <summary>The parser used for local files and remote schemas.</summary>
    private readonly IOpenApiDocumentParser _parser;

    /// <summary>Records failures encountered while opening a preview.</summary>
    private readonly ILogger<SchemaPreviewer> _logger;

    /// <summary>Creates a preview reader using the configured OpenAPI parser.</summary>
    /// <param name="parser">The parser for local and remote OpenAPI sources.</param>
    /// <param name="logger">The logger for preview failures.</param>
    public SchemaPreviewer(IOpenApiDocumentParser parser, ILogger<SchemaPreviewer> logger)
    {
        _parser = parser;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SchemaPreview?> PreviewAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var doc = Uri.TryCreate(path, UriKind.Absolute, out _)
                ? await _parser.ParseFromUrlAsync(path)
                : await _parser.ParseFromFileAsync(path);

            var size = File.Exists(path) ? new FileInfo(path).Length : 0;
            var operationCount = doc.Paths?.Sum(p => p.Value.Operations?.Count ?? 0) ?? 0;

            return new SchemaPreview(
                Title: doc.Info?.Title ?? "(untitled)",
                Version: doc.Info?.Version ?? "?",
                Paths: doc.Paths?.Count ?? 0,
                Operations: operationCount,
                Components: doc.Components?.Schemas?.Count ?? 0,
                FileSizeBytes: size);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Schema preview failed for {Path}", path);
            return null;
        }
    }
}
