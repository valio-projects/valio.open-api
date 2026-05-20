using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Builders;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.Ruby.Options;

namespace ValIO.Generators.Ruby.Tests.TestHelpers;

/// <summary>
/// Reusable harness for running the Ruby plugin pipeline against an
/// OpenAPI document loaded from <c>TestData/*.json</c>.
/// </summary>
public sealed class RubyPipelineTestHarness : IDisposable
{
    private readonly ServiceProvider _services;

    public RubyPipelineTestHarness()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INamingConverter, NamingConverter>();
        services.AddSingleton<IGeneratorPlugin, RubyGeneratorPlugin>();
        services.AddSingleton<IGeneratorRegistry>(sp =>
            new GeneratorRegistry(sp.GetServices<IGeneratorPlugin>()));
        // Plugin owns its registrations (mirrors AddGeneratorPlugin<T>()).
        new RubyGeneratorPlugin().ConfigureServices(services);
        _services = services.BuildServiceProvider();
    }

    public IServiceProvider Services => _services;

    public static async Task<OpenApiDocument> LoadDocumentAsync(string resourceName)
    {
        var assembly = typeof(RubyPipelineTestHarness).Assembly;
        var fullName = $"ValIO.Generators.Ruby.Tests.TestData.{resourceName}";
        await using var stream = assembly.GetManifestResourceStream(fullName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {fullName}");
        var result = await OpenApiDocument.LoadAsync(stream, "json");
        return result.Document ?? throw new InvalidOperationException("Failed to parse OpenAPI document");
    }

    public async Task<GenerationContext> RunAsync(
        OpenApiDocument document,
        RubyGenerationOptions? opts = null)
    {
        opts ??= new RubyGenerationOptions();
        var namingConverter = _services.GetRequiredService<INamingConverter>();
        var schemaResolver = new SchemaResolver(document);
        var irBuilder = new IrBuilder(schemaResolver, namingConverter);
        var irDocument = irBuilder.BuildFromOpenApi(document);

        var config = new GenerationConfiguration
        {
            LanguageId = "ruby",
            GenerateModels = true,
            GenerateClients = true,
            GenerateEnums = true,
            PluginOptions = opts,
        };

        var context = new GenerationContext
        {
            IrDocument = irDocument,
            Configuration = config,
            PluginOptions = opts,
        };

        var registry = _services.GetRequiredService<IGeneratorRegistry>();
        var plugin = registry.FindPlugin("ruby")!;
        var pipeline = plugin.CreatePipeline(_services);
        await pipeline.ExecuteAsync(context);
        return context;
    }

    public void Dispose() => _services.Dispose();
}
