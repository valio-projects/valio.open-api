using Microsoft.OpenApi;

namespace ValIO.Parsers.Interfaces;

/// <summary>
/// Интерфейс для парсинга OpenAPI документов
/// </summary>
public interface IOpenApiDocumentParser
{
    Task<OpenApiDocument> ParseFromFileAsync(string filePath);
    Task<OpenApiDocument> ParseFromUrlAsync(string url);
    Task<OpenApiDocument> ParseFromStreamAsync(Stream stream, string? format);
}

