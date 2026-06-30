using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Verifies the comprehensive JSDoc pass: every generated file carries an <c>@file</c>/<c>@module</c>/
/// <c>@generated</c> header, and the previously-undocumented declarations (enum union/values,
/// constraints, schema output aliases) now carry JSDoc.
/// </summary>
[TestFixture]
public class JsDocGenerationTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    private static string File(GenerationContext ctx, string endsWith) =>
        ctx.GeneratedFiles.First(f => f.RelativePath.Replace('\\', '/').EndsWith(endsWith, StringComparison.Ordinal)).Content;

    private static void ShouldHaveHeader(string content, string fileName, string module)
    {
        content.Should().StartWith("/**");
        content.Should().Contain($"@file {fileName}");
        content.Should().Contain($"@module {module}");
        content.Should().Contain("@generated");
        content.Should().Contain("Do not edit by hand");
    }

    [Test]
    public async Task ModelEnumResults_HaveFileHeaders()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        ShouldHaveHeader(File(ctx, "models/user/user.ts"), "user.ts", "models/user");
        ShouldHaveHeader(File(ctx, "models/userRole/userRole.ts"), "userRole.ts", "models/userRole");
        ShouldHaveHeader(File(ctx, "getUserById.results.ts"), "getUserById.results.ts", "endpoints/getUserById");
    }

    [Test]
    public async Task SchemasAndClients_HaveFileHeaders()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["ky"],
            ValidationLibrary = "zod3",
            MockLibrary = "msw",
        });

        ShouldHaveHeader(File(ctx, "models/user/user.schema.ts"), "user.schema.ts", "models/user");
        ShouldHaveHeader(File(ctx, "getUserById.ky.ts"), "getUserById.ky.ts", "endpoints/getUserById");
        ShouldHaveHeader(File(ctx, "getUserById.params.schema.ts"), "getUserById.params.schema.ts", "getUserById.params.schema");
        ShouldHaveHeader(File(ctx, "mocks/handlers.ts"), "handlers.ts", "mocks/handlers");
        ShouldHaveHeader(File(ctx, "index.ts"), "index.ts", "index");
    }

    [Test]
    public async Task EnumModel_UnionAndValues_AreDocumented()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var enumFile = File(ctx, "models/userRole/userRole.ts");
        // Union alias carries @enum + allowed-values remark.
        enumFile.Should().Contain("@enum {string}");
        enumFile.Should().Contain("Allowed values:");
        // Values const + type guard are documented.
        enumFile.Should().Contain("All valid UserRole values");
        enumFile.Should().Contain("Type guard");
    }

    [Test]
    public async Task ValibotSchemaOutputAlias_IsDocumented()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "valibot",
        });

        File(ctx, "models/userRole/userRole.schema.ts").Should().Contain("@typedef");
    }
}
