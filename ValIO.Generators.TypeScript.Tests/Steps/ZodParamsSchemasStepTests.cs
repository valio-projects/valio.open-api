using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Unit tests for <c>ZodParamsSchemasStep</c> — emits
/// <c>{operation}.params.schema.ts</c> with Route/Query Zod schemas
/// when validation is zod3 or zod4 and the operation has route/query params.
/// </summary>
[TestFixture]
public class ZodParamsSchemasStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task NoZodValidation_NoParamsSchemaFiles()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        ctx.GeneratedFiles.Should().NotContain(f =>
            f.RelativePath.EndsWith(".params.schema.ts", StringComparison.Ordinal));
    }

    [Test]
    public async Task Zod3WithPathParam_EmitsRouteParamsSchema()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        var paramsSchema = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.EndsWith("getUserById.params.schema.ts", StringComparison.Ordinal));
        paramsSchema.Should().NotBeNull();
        paramsSchema!.Content.Should().Contain("GetUserByIdRouteParamsSchema");
    }

    [Test]
    public async Task Zod4_UsesZodV4ImportPath()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod4",
        });

        var paramsSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUserById.params.schema.ts", StringComparison.Ordinal));
        paramsSchema.Content.Should().Contain("from 'zod/v4'");
    }

    [Test]
    public async Task OperationWithoutPathOrQueryParams_SkipsParamsSchemaFile()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        // getUsers has no path/query — skip.
        var skipped = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.EndsWith("getUsers.params.schema.ts", StringComparison.Ordinal));
        skipped.Should().BeNull();
    }
}
