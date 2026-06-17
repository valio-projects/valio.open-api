using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Builders;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Tests.TestHelpers;

/// <summary>
/// Reusable harness for running the TypeScript plugin pipeline against
/// an OpenAPI document loaded from embedded TestData/*.json.
/// </summary>
public sealed class PipelineTestHarness : IDisposable
{
    private readonly ServiceProvider _services;

    public PipelineTestHarness()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INamingConverter, NamingConverter>();
        services.AddSingleton<IGeneratorPlugin, TypeScriptGeneratorPlugin>();
        services.AddSingleton<IGeneratorRegistry>(sp =>
            new GeneratorRegistry(sp.GetServices<IGeneratorPlugin>()));
        // Mirror ServiceCollectionExtensions.AddGeneratorPlugin: the plugin owns its
        // service registrations (including the ITypeScriptNodeRenderer chain).
        new TypeScriptGeneratorPlugin().ConfigureServices(services);
        _services = services.BuildServiceProvider();
    }

    public IServiceProvider Services => _services;

    public static async Task<OpenApiDocument> LoadDocumentAsync(string resourceName)
    {
        var assembly = typeof(PipelineTestHarness).Assembly;
        var fullName = $"ValIO.Generators.TypeScript.Tests.TestData.{resourceName}";
        await using var stream = assembly.GetManifestResourceStream(fullName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {fullName}");
        var result = await OpenApiDocument.LoadAsync(stream, "json");
        return result.Document ?? throw new InvalidOperationException("Failed to parse OpenAPI document");
    }

    public static async Task<OpenApiDocument> LoadDocumentFromJsonAsync(string json)
    {
        await using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var result = await OpenApiDocument.LoadAsync(ms, "json");
        return result.Document ?? throw new InvalidOperationException("Failed to parse OpenAPI document");
    }

    public async Task<GenerationContext> RunAsync(
        OpenApiDocument document,
        TypeScriptGenerationOptions opts,
        FileOrganization? fileOrganization = null,
        NamingConventions? naming = null)
    {
        var namingConverter = _services.GetRequiredService<INamingConverter>();
        var schemaResolver = new SchemaResolver(document);
        var irBuilder = new IrBuilder(schemaResolver, namingConverter);
        var affix = naming is null
            ? NameAffixOptions.None
            : new NameAffixOptions
            {
                ModelPrefix = naming.ModelPrefix,
                ModelSuffix = naming.ModelSuffix,
                MethodPrefix = naming.MethodPrefix,
                MethodSuffix = naming.MethodSuffix,
                EnumPrefix = naming.EnumPrefix,
                EnumSuffix = naming.EnumSuffix,
            };
        var irDocument = irBuilder.BuildFromOpenApi(document, warnings: null, affixOptions: affix);

        var config = new GenerationConfiguration
        {
            LanguageId = "typescript",
            GenerateModels = true,
            GenerateClients = true,
            GenerateEnums = true,
            PluginOptions = opts,
            NamingConvention = naming ?? new NamingConventions(),
            FileOrganization = fileOrganization ?? new FileOrganization
            {
                SeparateModels = true,
                SeparateClients = true,
                ModelsPath = "models",
                ClientsPath = "api",
            },
        };

        var context = new GenerationContext
        {
            IrDocument = irDocument,
            Configuration = config,
            PluginOptions = opts,
        };

        var registry = _services.GetRequiredService<IGeneratorRegistry>();
        var plugin = registry.FindPlugin("typescript")!;
        var pipeline = plugin.CreatePipeline(_services);
        await pipeline.ExecuteAsync(context);
        return context;
    }

    public void Dispose() => _services.Dispose();
}
