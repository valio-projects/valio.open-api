using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Builders;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Parsers.Interfaces;

namespace ValIO.Application.Services;

/// <summary>
/// Default orchestrator. Stateless — same instance is safe for concurrent generation requests
/// (each call gets its own <see cref="GenerationContext"/>).
/// </summary>
public sealed class GenerationOrchestrator : IGenerationOrchestrator
{
    private readonly IOpenApiDocumentParser _parser;
    private readonly IGeneratorRegistry _registry;
    private readonly INamingConverter _namingConverter;
    private readonly ILogger<GenerationOrchestrator> _logger;
    private readonly IServiceProvider _services;

    public GenerationOrchestrator(
        IOpenApiDocumentParser parser,
        IGeneratorRegistry registry,
        INamingConverter namingConverter,
        ILogger<GenerationOrchestrator> logger,
        IServiceProvider services)
    {
        _parser = parser;
        _registry = registry;
        _namingConverter = namingConverter;
        _logger = logger;
        _services = services;
    }

    public async Task<OrchestrationResult> GenerateAsync(
        GenerationConfiguration config,
        string schemaPath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(schemaPath))
            return OrchestrationResult.Failure("Schema path is required");

        var plugin = _registry.FindPlugin(config.LanguageId);
        if (plugin is null)
            return OrchestrationResult.Failure($"No generator found for language: {config.LanguageId}");

        _logger.LogInformation("Using generator: {DisplayName}", plugin.DisplayName);
        _logger.LogInformation("Parsing OpenAPI schema from: {Schema}", schemaPath);

        OpenApiDocument doc;
        try
        {
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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to parse OpenAPI schema");
            return OrchestrationResult.Failure($"Failed to parse OpenAPI schema: {ex.Message}");
        }

        _logger.LogInformation("OpenAPI document parsed successfully");

        var schemaResolver = new CachingSchemaResolver(new SchemaResolver(doc));
        var irBuilder = new IrBuilder(schemaResolver, _namingConverter);
        var warnings = new List<string>();
        var affixOptions = BuildAffixOptions(config.NamingConvention);
        var irDocument = irBuilder.BuildFromOpenApi(doc, warnings, affixOptions);

        foreach (var w in warnings)
            _logger.LogWarning("{Warning}", w);

        _logger.LogInformation(
            "IR built: {Models} models, {Enums} enums, {Clients} clients",
            irDocument.Models.Count, irDocument.Enums.Count, irDocument.Clients.Count);

        var context = new GenerationContext
        {
            IrDocument = irDocument,
            Configuration = config,
            PluginOptions = config.PluginOptions,
        };

        try
        {
            var pipeline = plugin.CreatePipeline(_services);
            await pipeline.ExecuteAsync(context, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generator pipeline threw");
            return OrchestrationResult.Failure($"Generator pipeline failed: {ex.Message}");
        }

        _logger.LogInformation("Generated {Count} files", context.GeneratedFiles.Count);

        return new OrchestrationResult(
            Success: true,
            Files: context.GeneratedFiles.ToArray(),
            Warnings: warnings,
            ErrorMessage: null);
    }

    /// <summary>
    /// Переносит настраиваемые prefix/suffix имён моделей/методов/enum'ов из конфигурации
    /// в IR-преобразование. Result/response-type affix (TypeScript) применяется в самом
    /// генераторе, поэтому здесь не участвует.
    /// </summary>
    private static NameAffixOptions BuildAffixOptions(NamingConventions naming)
        => new()
        {
            ModelPrefix = naming.ModelPrefix,
            ModelSuffix = naming.ModelSuffix,
            MethodPrefix = naming.MethodPrefix,
            MethodSuffix = naming.MethodSuffix,
            EnumPrefix = naming.EnumPrefix,
            EnumSuffix = naming.EnumSuffix,
        };
}
