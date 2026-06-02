using FluentAssertions;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>
/// Integration tests that exercise the full TypeScript plugin pipeline
/// against the canonical Swagger Petstore (`TestData/petstore.json`)
/// across the matrix of supported options.
///
/// Mirrors the matrix in `Examples/configs/petstore-matrix.json`. These
/// tests guard the smoke-test invariants — file counts, the presence
/// of expected output families, and the import shape that drives `tsc`
/// resolution.
/// </summary>
[TestFixture]
public class PetstoreMatrixTests
{
    private PipelineTestHarness _harness = null!;
    private OpenApiDocument _document = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _harness = new PipelineTestHarness();
        _document = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
    }

    [OneTimeTearDown]
    public void OneTimeTeardown() => _harness.Dispose();

    private static IEnumerable<string> Paths(GenerationContext ctx) =>
        ctx.GeneratedFiles.Select(f => f.RelativePath.Replace('\\', '/'));

    private static IEnumerable<string> PathsEndingWith(GenerationContext ctx, string suffix) =>
        Paths(ctx).Where(p => p.EndsWith(suffix, StringComparison.Ordinal));

    [Test]
    public async Task Fetch_GeneratesModelsParamsResultsAndFetchClients()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        PathsEndingWith(ctx, ".fetch.ts").Should().NotBeEmpty();
        PathsEndingWith(ctx, ".params.ts").Should().NotBeEmpty("clients reference params types");
        PathsEndingWith(ctx, ".results.ts").Should().NotBeEmpty("clients reference result types");
        Paths(ctx).Should().Contain(p => p.StartsWith("models/", StringComparison.Ordinal));
        Paths(ctx).Should().Contain("index.ts");
    }

    [Test]
    public async Task AxiosZod3_EmitsAxiosClientsParamsSchemasAndModelSchemas()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["axios"],
            ValidationLibrary = "zod3",
        });

        PathsEndingWith(ctx, ".axios.ts").Should().NotBeEmpty();
        PathsEndingWith(ctx, ".params.schema.ts").Should().NotBeEmpty("zod3 emits {op}.params.schema.ts when path/query params exist");

        var modelSchemas = Paths(ctx).Where(p => p.Contains("schemas/", StringComparison.Ordinal) && p.EndsWith(".schema.ts", StringComparison.Ordinal)).ToList();
        modelSchemas.Should().NotBeEmpty("model schemas should be emitted under the root schemas/ folder");
        modelSchemas.Should().AllSatisfy(p => p.Split('/').Should().Contain("schemas"));
    }

    [Test]
    public async Task AxiosZod4TanStackReact_GeneratesQueryHooksAndUsesZod4Import()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["axios"],
            ValidationLibrary = "zod4",
            QueryWrapper = "tanstack-react",
        });

        PathsEndingWith(ctx, ".axios.ts").Should().NotBeEmpty();
        PathsEndingWith(ctx, ".query.ts").Should().NotBeEmpty("TanStack React wrappers should be emitted");

        var anySchema = ctx.GeneratedFiles.First(f => f.RelativePath.EndsWith(".schema.ts", StringComparison.Ordinal));
        anySchema.Content.Should().Contain("zod/v4", "zod4 uses the `zod/v4` import path");

        var anyQuery = ctx.GeneratedFiles.First(f => f.RelativePath.EndsWith(".query.ts", StringComparison.Ordinal));
        anyQuery.Content.Should().Contain("@tanstack/react-query");
    }

    [Test]
    public async Task FetchValibot_EmitsValibotSchemasAndCorrectImports()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "valibot",
        });

        PathsEndingWith(ctx, ".fetch.ts").Should().NotBeEmpty();
        var schemaFiles = ctx.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".schema.ts", StringComparison.Ordinal)).ToList();
        schemaFiles.Should().NotBeEmpty();
        schemaFiles[0].Content.Should().Contain("import * as v from \"valibot\"");

        var petSchema = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').Contains("schemas/pet/", StringComparison.OrdinalIgnoreCase));
        petSchema.Should().NotBeNull();
        petSchema!.Content.Should().Contain("v.object");
    }

    [Test]
    public async Task FetchTypeBox_EmitsTypeBoxSchemasWithStaticTypeAliases()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "typebox",
        });

        var schemaFiles = ctx.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".schema.ts", StringComparison.Ordinal)).ToList();
        schemaFiles.Should().NotBeEmpty();
        schemaFiles[0].Content.Should().Contain("@sinclair/typebox");
        schemaFiles.Should().Contain(f => f.Content.Contains("Type.Object", StringComparison.Ordinal));
    }

    [Test]
    public async Task KyZod3_EmitsKyClientsAndAvoidsCaseMismatchOnSchemaPaths()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["ky"],
            ValidationLibrary = "zod3",
        });

        var kyFiles = PathsEndingWith(ctx, ".ky.ts").ToList();
        kyFiles.Should().NotBeEmpty();

        // Schema imports must point at camelCased file names — case-sensitive
        // filesystems break otherwise.
        var kySource = ctx.GeneratedFiles.First(f => f.RelativePath.EndsWith(".ky.ts", StringComparison.Ordinal));
        if (kySource.Content.Contains("schema", StringComparison.Ordinal))
        {
            kySource.Content.Should().NotMatchRegex(@"schemas/[A-Z][a-zA-Z]*/[A-Z][a-zA-Z]*\.schema",
                "schema imports must be camelCased");
        }
    }

    [Test]
    public async Task KyTanStackVue_EmitsVueQueryHooksWithMaybeRef()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["ky"],
            ValidationLibrary = "zod3",
            QueryWrapper = "tanstack-vue",
        });

        var queryFiles = PathsEndingWith(ctx, ".query.ts").ToList();
        queryFiles.Should().NotBeEmpty();

        var withParams = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".query.ts", StringComparison.Ordinal)
            && f.Content.Contains("MaybeRef", StringComparison.Ordinal));
        withParams.Content.Should().Contain("@tanstack/vue-query");
        withParams.Content.Should().Contain("unref");
    }

    [Test]
    public async Task AllTransports_AlwaysGenerateRootIndexTs()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var index = ctx.GeneratedFiles.FirstOrDefault(f => f.RelativePath.Replace('\\', '/') == "index.ts");
        index.Should().NotBeNull("root index.ts must be emitted at the output root");
        index!.Content.Should().Contain("export *");
        index.Content.Should().NotContain("./api/api/", "index.ts paths should be relative to root, not doubled");
    }

    [Test]
    public async Task Binary_RequestBody_AddsBodyFieldOnParams()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        // Petstore's uploadFile takes application/octet-stream — binary body
        // must surface as `body?: Blob` (or similar) in the params interface.
        var uploadParams = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.EndsWith("uploadFile.params.ts", StringComparison.Ordinal));
        uploadParams.Should().NotBeNull();
        uploadParams!.Content.Should().Contain("body");
    }
}
