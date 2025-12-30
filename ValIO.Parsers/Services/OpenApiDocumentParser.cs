using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using ValIO.Parsers.Interfaces;

namespace ValIO.Parsers.Services;

/// <summary>
/// Парсер OpenAPI документов
/// </summary>
public class OpenApiDocumentParser : IOpenApiDocumentParser
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenApiDocumentParser> _logger;

    public OpenApiDocumentParser(IHttpClientFactory httpClientFactory, ILogger<OpenApiDocumentParser> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OpenApiDocument> ParseFromFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"OpenAPI file not found: {filePath}");

        var format = DetectFormat(filePath);
        _logger.LogInformation("Parsing OpenAPI file: {FilePath}, Format: {Format}", filePath, format);

        await using var stream = File.OpenRead(filePath);
        return await ParseFromStreamAsync(stream, format);
    }

    public async Task<OpenApiDocument> ParseFromUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid URL: {url}", nameof(url));

        _logger.LogInformation("Downloading OpenAPI document from: {Url}", url);

        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(uri);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Failed to download OpenAPI document: {response.StatusCode}");

        var format = DetectFormatFromContentType(response.Content.Headers.ContentType?.MediaType);
        
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await ParseFromStreamAsync(stream, format);
    }

    public async Task<OpenApiDocument> ParseFromStreamAsync(Stream stream, string? format)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        // Если формат не указан, пытаемся определить
        if (string.IsNullOrEmpty(format))
        {
            format = DetectFormatFromStream(stream);
        }

        var readResult = await OpenApiDocument.LoadAsync(stream, format);

        if (readResult.Document == null)
        {
            var errors = readResult.Diagnostic?.Errors.Any() == true
                ? string.Join(", ", readResult.Diagnostic.Errors.Select(e => e.Message))
                : "Unknown error";
            _logger.LogError("OpenAPI parsing errors: {Errors}", errors);
            throw new InvalidOperationException($"Failed to parse OpenAPI document: {errors}");
        }

        if (readResult.Diagnostic?.Warnings.Any() == true)
        {
            var warnings = string.Join(", ", readResult.Diagnostic.Warnings.Select(w => w.Message));
            _logger.LogWarning("OpenAPI parsing warnings: {Warnings}", warnings);
        }

        return readResult.Document;
    }

    private static string DetectFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            _ => "json" // По умолчанию JSON
        };
    }

    private static string? DetectFormatFromContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        return contentType.ToLowerInvariant() switch
        {
            var ct when ct.Contains("json") => "json",
            var ct when ct.Contains("yaml") || ct.Contains("yml") => "yaml",
            _ => null
        };
    }

    private string DetectFormatFromStream(Stream stream)
    {
        var position = stream.Position;
        stream.Position = 0;

        try
        {
            var buffer = new byte[1024];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            var content = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimStart();

            // JSON начинается с { или [
            if (content.StartsWith('{') || content.StartsWith('['))
                return "json";

            // YAML обычно начинается с --- или без специальных символов
            return "yaml";
        }
        finally
        {
            stream.Position = position;
        }
    }
}

