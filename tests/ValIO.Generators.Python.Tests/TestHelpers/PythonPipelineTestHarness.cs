using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Builders;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.Python;
using ValIO.Generators.Python.Ast.Renderers;
using ValIO.Generators.Python.Options;

namespace ValIO.Generators.Python.Tests.TestHelpers;

/// <summary>
/// Хост для прогона Python-плагина по embedded OpenAPI-документу.
/// </summary>
public sealed class PythonPipelineTestHarness : IDisposable
{
    private readonly ServiceProvider _services;

    public PythonPipelineTestHarness()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INamingConverter, NamingConverter>();
        services.AddSingleton<PythonIrTypeMapper>();
        services.AddSingleton<IPythonAstRenderer, PythonAstRenderer>();
        services.AddSingleton<IGeneratorPlugin, PythonGeneratorPlugin>();
        services.AddSingleton<IGeneratorRegistry>(sp =>
            new GeneratorRegistry(sp.GetServices<IGeneratorPlugin>()));
        _services = services.BuildServiceProvider();
    }

    public IServiceProvider Services => _services;

    public async Task<GenerationContext> RunAsync(
        OpenApiDocument document,
        PythonGenerationOptions? opts = null)
    {
        opts ??= new PythonGenerationOptions();
        var namingConverter = _services.GetRequiredService<INamingConverter>();
        var schemaResolver = new SchemaResolver(document);
        var irBuilder = new IrBuilder(schemaResolver, namingConverter);
        var irDocument = irBuilder.BuildFromOpenApi(document);

        var config = new GenerationConfiguration
        {
            LanguageId = "python",
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
        var plugin = registry.FindPlugin("python")!;
        var pipeline = plugin.CreatePipeline(_services);
        await pipeline.ExecuteAsync(context);
        return context;
    }

    public void Dispose() => _services.Dispose();
}
