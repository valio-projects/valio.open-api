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

    /// <summary>Reports the outcome and source counts from validating an OpenAPI document.</summary>
    public sealed class ValidateResponse : IEquatable<ValidateResponse>
    {
        /// <summary>Initializes the values carried by this model.</summary>
        /// <param name="IsValid">Whether validation succeeded.</param>
        /// <param name="Title">The document title, when available.</param>
        /// <param name="Version">The declared document version, when available.</param>
        /// <param name="PathCount">The number of OpenAPI paths.</param>
        /// <param name="OperationCount">The number of HTTP operations.</param>
        /// <param name="ComponentSchemaCount">The number of reusable component schemas.</param>
        /// <param name="Warnings">The validation warnings in reader order.</param>
        /// <param name="Error">The validation error, when present.</param>
        public ValidateResponse(
            bool IsValid,
            string? Title,
            string? Version,
            int PathCount,
            int OperationCount,
            int ComponentSchemaCount,
            IReadOnlyList<string> Warnings,
            string? Error)
        {
            this.IsValid = IsValid;
            this.Title = Title;
            this.Version = Version;
            this.PathCount = PathCount;
            this.OperationCount = OperationCount;
            this.ComponentSchemaCount = ComponentSchemaCount;
            this.Warnings = Warnings;
            this.Error = Error;
        }

        /// <summary>Gets whether validation succeeded.</summary>
        /// <value>Whether validation succeeded.</value>
        public bool IsValid { get; init; }

        /// <summary>Gets the document title, when available.</summary>
        /// <value>The document title, when available.</value>
        public string? Title { get; init; }

        /// <summary>Gets the declared document version, when available.</summary>
        /// <value>The declared document version, when available.</value>
        public string? Version { get; init; }

        /// <summary>Gets the number of OpenAPI paths.</summary>
        /// <value>The number of OpenAPI paths.</value>
        public int PathCount { get; init; }

        /// <summary>Gets the number of HTTP operations.</summary>
        /// <value>The number of HTTP operations.</value>
        public int OperationCount { get; init; }

        /// <summary>Gets the number of reusable component schemas.</summary>
        /// <value>The number of reusable component schemas.</value>
        public int ComponentSchemaCount { get; init; }

        /// <summary>Gets the validation warnings in reader order.</summary>
        /// <value>The validation warnings in reader order.</value>
        public IReadOnlyList<string> Warnings { get; init; }

        /// <summary>Gets the validation error, when present.</summary>
        /// <value>The validation error, when present.</value>
        public string? Error { get; init; }

        /// <summary>Determines whether another value has the same stored members.</summary>
        /// <param name="other">The instance to compare.</param>
        /// <returns>True when all stored members are equal.</returns>
        public bool Equals(ValidateResponse? other) => other is not null
            && EqualityComparer<bool>.Default.Equals(IsValid, other.IsValid)
            && EqualityComparer<string?>.Default.Equals(Title, other.Title)
            && EqualityComparer<string?>.Default.Equals(Version, other.Version)
            && EqualityComparer<int>.Default.Equals(PathCount, other.PathCount)
            && EqualityComparer<int>.Default.Equals(OperationCount, other.OperationCount)
            && EqualityComparer<int>.Default.Equals(ComponentSchemaCount, other.ComponentSchemaCount)
            && EqualityComparer<IReadOnlyList<string>>.Default.Equals(Warnings, other.Warnings)
            && EqualityComparer<string?>.Default.Equals(Error, other.Error);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is ValidateResponse other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(IsValid, Title, Version, PathCount, OperationCount, ComponentSchemaCount, Warnings, Error);

        /// <summary>Compares two values for equality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when both are null or carry the same member values.</returns>
        public static bool operator ==(ValidateResponse? left, ValidateResponse? right) => Equals(left, right);

        /// <summary>Compares two values for inequality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when the values differ.</returns>
        public static bool operator !=(ValidateResponse? left, ValidateResponse? right) => !Equals(left, right);
    }

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
