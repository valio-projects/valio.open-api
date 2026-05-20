using Microsoft.OpenApi;
using ValIO.Parsers.Interfaces;
using ValIO.Web.Services;

namespace ValIO.Web.Endpoints;

/// <summary>
/// POST /api/validate — parse the OpenAPI schema and return a summary
/// (title, version, paths/components counts) without running any generator.
/// Companion to <c>valio validate</c>.
/// </summary>
public static class ValidateEndpoint
{
    public sealed class ValidateRequest
    {
        public string? SchemaUrl { get; set; }
        public string? SchemaFileName { get; set; }
        public string? SchemaContentBase64 { get; set; }
    }

    public sealed record ValidateResponse(
        bool IsValid,
        string? Title,
        string? Version,
        int PathCount,
        int OperationCount,
        int ComponentSchemaCount,
        IReadOnlyList<string> Warnings,
        string? Error);

    public static async Task<IResult> HandleAsync(
        ValidateRequest req,
        IOpenApiDocumentParser parser,
        ISafeSchemaFetcher fetcher,
        CancellationToken ct)
    {
        string schemaPath;
        try
        {
            if (!string.IsNullOrWhiteSpace(req.SchemaUrl))
                schemaPath = await fetcher.PrepareUrlAsync(req.SchemaUrl, ct);
            else if (!string.IsNullOrWhiteSpace(req.SchemaContentBase64))
            {
                var bytes = Convert.FromBase64String(req.SchemaContentBase64);
                schemaPath = await fetcher.PrepareUploadAsync(req.SchemaFileName ?? "schema.json", bytes, ct);
            }
            else
            {
                return Results.BadRequest("Either schemaUrl or schemaContentBase64 is required");
            }
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        try
        {
            OpenApiDocument doc = Uri.TryCreate(schemaPath, UriKind.Absolute, out _)
                ? await parser.ParseFromUrlAsync(schemaPath)
                : await parser.ParseFromFileAsync(schemaPath);

            var operationCount = doc.Paths?.Sum(p => p.Value.Operations?.Count ?? 0) ?? 0;
            var schemaCount = doc.Components?.Schemas?.Count ?? 0;

            return Results.Ok(new ValidateResponse(
                IsValid: true,
                Title: doc.Info?.Title,
                Version: doc.Info?.Version,
                PathCount: doc.Paths?.Count ?? 0,
                OperationCount: operationCount,
                ComponentSchemaCount: schemaCount,
                Warnings: Array.Empty<string>(),
                Error: null));
        }
        catch (Exception ex)
        {
            return Results.Ok(new ValidateResponse(
                IsValid: false,
                Title: null,
                Version: null,
                PathCount: 0,
                OperationCount: 0,
                ComponentSchemaCount: 0,
                Warnings: Array.Empty<string>(),
                Error: ex.Message));
        }
    }
}
