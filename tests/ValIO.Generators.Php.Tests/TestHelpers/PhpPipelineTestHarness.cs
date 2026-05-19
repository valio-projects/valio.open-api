using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Builders;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.Php.Options;

namespace ValIO.Generators.Php.Tests.TestHelpers;

/// <summary>
/// Reusable harness for running the PHP plugin pipeline against an OpenAPI document.
/// Mirror of <c>PipelineTestHarness</c> from the TypeScript test project.
/// </summary>
public sealed class PhpPipelineTestHarness : IDisposable
{
    private readonly ServiceProvider _services;

    public PhpPipelineTestHarness()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INamingConverter, NamingConverter>();
        services.AddSingleton<IGeneratorPlugin, PhpGeneratorPlugin>();
        services.AddSingleton<IGeneratorRegistry>(sp =>
            new GeneratorRegistry(sp.GetServices<IGeneratorPlugin>()));
        _services = services.BuildServiceProvider();
    }

    public IServiceProvider Services => _services;

    public static async Task<OpenApiDocument> LoadDocumentAsync(string resourceName)
    {
        var assembly = typeof(PhpPipelineTestHarness).Assembly;
        var fullName = $"ValIO.Generators.Php.Tests.TestData.{resourceName}";
        await using var stream = assembly.GetManifestResourceStream(fullName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {fullName}");
        var result = await OpenApiDocument.LoadAsync(stream, "json");
        return result.Document ?? throw new InvalidOperationException("Failed to parse OpenAPI document");
    }

    public async Task<GenerationContext> RunAsync(
        OpenApiDocument document,
        PhpGenerationOptions? opts = null,
        FileOrganization? fileOrganization = null)
    {
        opts ??= new PhpGenerationOptions();
        var namingConverter = _services.GetRequiredService<INamingConverter>();
        var schemaResolver = new SchemaResolver(document);
        var irBuilder = new IrBuilder(schemaResolver, namingConverter);
        var irDocument = irBuilder.BuildFromOpenApi(document);

        var config = new GenerationConfiguration
        {
            LanguageId = "php",
            GenerateModels = true,
            GenerateClients = true,
            GenerateEnums = true,
            PluginOptions = opts,
            FileOrganization = fileOrganization ?? new FileOrganization
            {
                SeparateModels = true,
                SeparateClients = true,
                ModelsPath = "Models",
                ClientsPath = "Api",
            },
        };

        var context = new GenerationContext
        {
            IrDocument = irDocument,
            Configuration = config,
            PluginOptions = opts,
        };

        var registry = _services.GetRequiredService<IGeneratorRegistry>();
        var plugin = registry.FindPlugin("php")!;
        var pipeline = plugin.CreatePipeline(_services);
        await pipeline.ExecuteAsync(context);
        return context;
    }

    public void Dispose() => _services.Dispose();
}
