using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Builders;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.Dart.Options;

namespace ValIO.Generators.Dart.Tests.TestHelpers;

/// <summary>
/// Reusable harness for running the Dart plugin pipeline against an
/// OpenAPI document loaded from the shared fixtures.
/// </summary>
public sealed class DartPipelineTestHarness : IDisposable
{
    private readonly ServiceProvider _services;

    public DartPipelineTestHarness()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INamingConverter, NamingConverter>();
        services.AddSingleton<IGeneratorPlugin, DartGeneratorPlugin>();
        services.AddSingleton<IGeneratorRegistry>(sp =>
            new GeneratorRegistry(sp.GetServices<IGeneratorPlugin>()));
        new DartGeneratorPlugin().ConfigureServices(services);
        _services = services.BuildServiceProvider();
    }

    public IServiceProvider Services => _services;

    public async Task<GenerationContext> RunAsync(
        OpenApiDocument document,
        DartGenerationOptions? opts = null)
    {
        opts ??= new DartGenerationOptions();
        var namingConverter = _services.GetRequiredService<INamingConverter>();
        var schemaResolver = new SchemaResolver(document);
        var irBuilder = new IrBuilder(schemaResolver, namingConverter);
        var irDocument = irBuilder.BuildFromOpenApi(document);

        var config = new GenerationConfiguration
        {
            LanguageId = "dart",
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
        var plugin = registry.FindPlugin("dart")!;
        var pipeline = plugin.CreatePipeline(_services);
        await pipeline.ExecuteAsync(context);
        return context;
    }

    public void Dispose() => _services.Dispose();
}
