using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Builders;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Parsers.Interfaces;

namespace ValIO.Services;

/// <summary>
/// Facade: скрывает сложность конвейера генерации от CLI.
/// Единственная точка входа: ParseAsync + BuildIr + ExecutePipeline + WriteFiles.
/// </summary>
public sealed class GenerationOrchestrator
{
    private readonly IOpenApiDocumentParser _parser;
    private readonly IGeneratorRegistry _registry;
    private readonly INamingConverter _namingConverter;
    private readonly IFileWriter _fileWriter;
    private readonly ILogger<GenerationOrchestrator> _logger;
    private readonly IServiceProvider _services;

    public GenerationOrchestrator(
        IOpenApiDocumentParser parser,
        IGeneratorRegistry registry,
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        ILogger<GenerationOrchestrator> logger,
        IServiceProvider services)
    {
        _parser = parser;
        _registry = registry;
        _namingConverter = namingConverter;
        _fileWriter = fileWriter;
        _logger = logger;
        _services = services;
    }

    public async Task<bool> GenerateAsync(
        GenerationConfiguration config,
        string schemaPath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(schemaPath))
        {
            _logger.LogError("Schema path is required");
            return false;
        }

        var plugin = _registry.FindPlugin(config.LanguageId);
        if (plugin == null)
        {
            _logger.LogError("No generator found for language: {LanguageId}", config.LanguageId);
            return false;
        }

        _logger.LogInformation("Using generator: {DisplayName}", plugin.DisplayName);
        _logger.LogInformation("Parsing OpenAPI schema from: {Schema}", schemaPath);

        OpenApiDocument doc;
        if (Uri.TryCreate(schemaPath, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase)))
        {
            doc = await _parser.ParseFromUrlAsync(schemaPath);
        }
        else
        {
            doc = await _parser.ParseFromFileAsync(schemaPath);
        }

        _logger.LogInformation("OpenAPI document parsed successfully");

        var schemaResolver = new CachingSchemaResolver(new SchemaResolver(doc));
        var irBuilder = new IrBuilder(schemaResolver, _namingConverter);
        var warnings = new List<string>();
        var irDocument = irBuilder.BuildFromOpenApi(doc, warnings);

        foreach (var w in warnings)
            _logger.LogWarning("{Warning}", w);

        _logger.LogInformation("IR built: {Models} models, {Enums} enums, {Clients} clients",
            irDocument.Models.Count, irDocument.Enums.Count, irDocument.Clients.Count);

        var context = new GenerationContext
        {
            IrDocument = irDocument,
            Configuration = config,
            PluginOptions = config.PluginOptions,
        };

        var pipeline = plugin.CreatePipeline(_services);
        await pipeline.ExecuteAsync(context, ct);

        _logger.LogInformation("Generated {Count} files", context.GeneratedFiles.Count);

        var outputPath = config.OutputPath ?? ".";
        _fileWriter.EnsureDirectoryExists(outputPath);

        foreach (var file in context.GeneratedFiles)
        {
            ct.ThrowIfCancellationRequested();
            var fullPath = Path.Combine(outputPath, file.RelativePath);
            _fileWriter.EnsureDirectoryExists(Path.GetDirectoryName(fullPath)!);
            await _fileWriter.WriteFileAsync(fullPath, file.Content);
            _logger.LogDebug("Written: {Path}", fullPath);
        }

        _logger.LogInformation("Code generation complete. Output: {Output}", outputPath);
        return true;
    }
}
