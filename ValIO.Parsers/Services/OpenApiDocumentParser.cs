using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using ValIO.Parsers.Interfaces;

namespace ValIO.Parsers.Services;

/// <summary>
/// Парсер OpenAPI документов
/// </summary>
public class OpenApiDocumentParser : IOpenApiDocumentParser
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenApiDocumentParser> _logger;
    private readonly OpenApiStreamReader _jsonReader;
    private readonly OpenApiStreamReader _yamlReader;

    public OpenApiDocumentParser(IHttpClientFactory httpClientFactory, ILogger<OpenApiDocumentParser> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jsonReader = new OpenApiStreamReader();
        _yamlReader = new OpenApiStreamReader();
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

    public Task<OpenApiDocument> ParseFromStreamAsync(Stream stream, string? format)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        var reader = format?.ToLowerInvariant() switch
        {
            "json" => _jsonReader,
            "yaml" or "yml" => _yamlReader,
            _ => DetectFormatFromStream(stream)
        };

        var parseResult = reader.Read(stream, out var diagnostic);

        if (diagnostic.Errors.Any())
        {
            var errors = string.Join(", ", diagnostic.Errors.Select(e => e.Message));
            _logger.LogError("OpenAPI parsing errors: {Errors}", errors);
            throw new InvalidOperationException($"Failed to parse OpenAPI document: {errors}");
        }

        if (parseResult == null)
            throw new InvalidOperationException("Failed to parse OpenAPI document: result is null");

        if (diagnostic.Warnings.Any())
        {
            var warnings = string.Join(", ", diagnostic.Warnings.Select(w => w.Message));
            _logger.LogWarning("OpenAPI parsing warnings: {Warnings}", warnings);
        }

        return Task.FromResult(parseResult);
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

    private OpenApiStreamReader DetectFormatFromStream(Stream stream)
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
                return _jsonReader;

            // YAML обычно начинается с --- или без специальных символов
            return _yamlReader;
        }
        finally
        {
            stream.Position = position;
        }
    }
}

