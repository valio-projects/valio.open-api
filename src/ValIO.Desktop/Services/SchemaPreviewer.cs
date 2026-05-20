using Microsoft.Extensions.Logging;
using ValIO.Parsers.Interfaces;

namespace ValIO.Desktop.Services;

public sealed class SchemaPreviewer(IOpenApiDocumentParser parser, ILogger<SchemaPreviewer> logger)
    : ISchemaPreviewer
{
    public async Task<SchemaPreview?> PreviewAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var doc = Uri.TryCreate(path, UriKind.Absolute, out _)
                ? await parser.ParseFromUrlAsync(path)
                : await parser.ParseFromFileAsync(path);

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
            logger.LogDebug(ex, "Schema preview failed for {Path}", path);
            return null;
        }
    }
}
