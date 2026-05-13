using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Builders;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Tests;

[TestFixture]
public class TypeScriptCodeGeneratorIntegrationTests
{
    private IServiceProvider _services = null!;
    private OpenApiDocument _document = null!;

    [SetUp]
    public async Task Setup()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INamingConverter, NamingConverter>();
        services.AddSingleton<IGeneratorPlugin, TypeScriptGeneratorPlugin>();
        services.AddSingleton<IGeneratorRegistry>(sp =>
            new GeneratorRegistry(sp.GetServices<IGeneratorPlugin>()));
        _services = services.BuildServiceProvider();

        var assembly = typeof(TypeScriptCodeGeneratorIntegrationTests).Assembly;
        await using var stream = assembly.GetManifestResourceStream(
            "ValIO.Generators.TypeScript.Tests.TestData.simple-schema.json")!;
        var result = await OpenApiDocument.LoadAsync(stream, "json");
        _document = result.Document ?? throw new InvalidOperationException("Failed to parse OpenAPI document");
    }

    private async Task<GenerationContext> RunPipeline(TypeScriptGenerationOptions opts)
    {
        var namingConverter = _services.GetRequiredService<INamingConverter>();
        var schemaResolver = new SchemaResolver(_document);
        var irBuilder = new IrBuilder(schemaResolver, namingConverter);
        var irDocument = irBuilder.BuildFromOpenApi(_document);

        var config = new GenerationConfiguration
        {
            LanguageId = "typescript",
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
        var plugin = registry.FindPlugin("typescript")!;
        var pipeline = plugin.CreatePipeline(_services);
        await pipeline.ExecuteAsync(context);
        return context;
    }

    [Test]
    public async Task GenerateModels_WithFetch_GeneratesInterfaces()
    {
        var opts = new TypeScriptGenerationOptions { Transports = ["fetch"] };
        var context = await RunPipeline(opts);

        var modelFiles = context.GeneratedFiles
            .Where(f => f.RelativePath.EndsWith(".ts") && f.RelativePath.Contains("models"))
            .ToList();

        modelFiles.Should().NotBeEmpty("should generate model files");

        var userFile = modelFiles.FirstOrDefault(f => f.RelativePath.Contains("user", StringComparison.OrdinalIgnoreCase));
        userFile.Should().NotBeNull("should generate User model");
        userFile!.Content.Should().Contain("export interface User");
        userFile.Content.Should().Contain("id:");
        userFile.Content.Should().Contain("name:");
        userFile.Content.Should().Contain("email:");
    }

    [Test]
    public async Task GenerateClients_WithFetch_GeneratesFetchClient()
    {
        var opts = new TypeScriptGenerationOptions { Transports = ["fetch"] };
        var context = await RunPipeline(opts);

        var fetchFiles = context.GeneratedFiles
            .Where(f => f.RelativePath.EndsWith(".fetch.ts"))
            .ToList();

        fetchFiles.Should().NotBeEmpty("should generate fetch client files");
        fetchFiles[0].Content.Should().Contain("fetch(");
    }

    [Test]
    public async Task GenerateClients_WithAxios_GeneratesAxiosClient()
    {
        var opts = new TypeScriptGenerationOptions { Transports = ["axios"] };
        var context = await RunPipeline(opts);

        var axiosFiles = context.GeneratedFiles
            .Where(f => f.RelativePath.EndsWith(".axios.ts"))
            .ToList();

        axiosFiles.Should().NotBeEmpty("should generate axios client files");
        axiosFiles[0].Content.Should().Contain("axios");
    }

    [Test]
    public async Task GenerateSchemas_WithZod3_GeneratesZodSchemas()
    {
        var opts = new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3"
        };
        var context = await RunPipeline(opts);

        var schemaFiles = context.GeneratedFiles
            .Where(f => f.RelativePath.EndsWith(".schema.ts"))
            .ToList();

        schemaFiles.Should().NotBeEmpty("should generate schema files with zod3");

        var userSchema = schemaFiles.FirstOrDefault(f => f.RelativePath.Contains("User", StringComparison.OrdinalIgnoreCase));
        userSchema.Should().NotBeNull("should generate User schema");
        userSchema!.Content.Should().Contain("z.object");
        userSchema.Content.Should().Contain("UserSchema");
    }

    [Test]
    public async Task GenerateIndex_AlwaysCreatesIndexTs()
    {
        var opts = new TypeScriptGenerationOptions { Transports = ["fetch"] };
        var context = await RunPipeline(opts);

        var indexFile = context.GeneratedFiles.FirstOrDefault(f => f.RelativePath.EndsWith("index.ts"));
        indexFile.Should().NotBeNull("should generate index.ts");
        indexFile!.Content.Should().Contain("export *");
    }

    [Test]
    public async Task GenerateEnums_GeneratesUnionTypes()
    {
        var opts = new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            EnumFormat = "union"
        };
        var context = await RunPipeline(opts);

        var enumFiles = context.GeneratedFiles
            .Where(f => f.RelativePath.Contains("UserRole", StringComparison.OrdinalIgnoreCase) && f.RelativePath.EndsWith(".ts"))
            .ToList();

        enumFiles.Should().NotBeEmpty("should generate UserRole enum file");
    }

    [Test]
    public async Task GenerateSchemas_WithZod4_UsesZod4Import()
    {
        var opts = new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod4"
        };
        var context = await RunPipeline(opts);

        var schemaFiles = context.GeneratedFiles
            .Where(f => f.RelativePath.EndsWith(".schema.ts"))
            .ToList();

        schemaFiles.Should().NotBeEmpty("should generate schema files with zod4");
        schemaFiles[0].Content.Should().Contain("zod/v4", "zod4 should use the zod/v4 import path");
    }

    [Test]
    public async Task GeneratorRegistry_FindsTypeScriptPlugin()
    {
        var registry = _services.GetRequiredService<IGeneratorRegistry>();

        registry.FindPlugin("typescript").Should().NotBeNull();
        registry.FindPlugin("ts").Should().NotBeNull();
        registry.FindPlugin("TYPESCRIPT").Should().NotBeNull("should be case-insensitive");
        registry.FindPlugin("php").Should().BeNull("unknown language should return null");
    }
}
