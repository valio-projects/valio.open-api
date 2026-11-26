using Microsoft.OpenApi;

namespace ValIO.Tests.Fixtures;

/// <summary>Loads OpenAPI fixtures copied beside each test assembly.</summary>
internal static class OpenApiFixture
{
    /// <summary>Reads the document exactly as it appears in the fixture file.</summary>
    /// <param name="relativePath">The path relative to the OpenAPI fixture directory.</param>
    /// <returns>The original JSON or YAML document.</returns>
    /// <exception cref="IOException">The fixture cannot be read.</exception>
    public static string Read(string relativePath) => FixtureFile.Read(Path.Combine("OpenApi", relativePath));

    /// <summary>Parses an OpenAPI fixture using the reader for its file extension.</summary>
    /// <param name="relativePath">The path relative to the OpenAPI fixture directory.</param>
    /// <param name="allowReaderErrors">Whether to accept a parsed document for a regression fixture with known reader diagnostics.</param>
    /// <returns>The parsed OpenAPI document.</returns>
    /// <exception cref="InvalidOperationException">The reader produced no document or reported unexpected errors.</exception>
    public static async Task<OpenApiDocument> LoadAsync(string relativePath, bool allowReaderErrors = false)
    {
        await using var stream = FixtureFile.Open(Path.Combine("OpenApi", relativePath));
        var format = Path.GetExtension(relativePath).TrimStart('.');
        var result = await OpenApiDocument.LoadAsync(stream, format);
        var errors = result.Diagnostic?.Errors.Select(error => error.Message).ToArray() ?? [];
        if (result.Document is null || (!allowReaderErrors && errors.Length > 0))
            throw new InvalidOperationException($"Unable to parse fixture '{relativePath}'. {string.Join(Environment.NewLine, errors)}");
        return result.Document;
    }
}
