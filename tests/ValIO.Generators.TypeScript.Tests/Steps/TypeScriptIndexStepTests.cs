using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Unit tests for <c>TypeScriptIndexStep</c> — barrel file with
/// <c>export *</c> for every emitted <c>.ts</c> file, relative to root.
/// </summary>
[TestFixture]
public class TypeScriptIndexStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    private static string IndexOf(GenerationContext ctx) =>
        ctx.GeneratedFiles.First(f => f.RelativePath.Replace('\\', '/') == "index.ts").Content;

    [Test]
    public async Task EmitsIndexAtRoot()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var index = ctx.GeneratedFiles.FirstOrDefault(f => f.RelativePath.Replace('\\', '/') == "index.ts");
        index.Should().NotBeNull("root index.ts must be emitted at the output root");
    }

    [Test]
    public async Task ReExportsAreRelativeToRootNotApi()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var index = IndexOf(ctx);
        // Regression: prior bug placed the index inside `api/` and prefixed every
        // re-export with `./api/...`, doubling the path. Now they're relative to root.
        index.Should().NotContain("./api/api/");
        index.Should().Contain("./api/");      // api/<op>/... files re-exported
        index.Should().Contain("./models/");   // models/<name>/... files re-exported
    }

    [Test]
    public async Task ReExportsHaveExportStarSyntax()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var index = IndexOf(ctx);
        index.Should().MatchRegex(@"export \* from ""\.\/[^""]+""");
    }

    [Test]
    public async Task NoSelfReferenceToOtherIndexFiles()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var index = IndexOf(ctx);
        // Should not export from itself or any nested index.ts (e.g. api/index.ts).
        index.Should().NotMatchRegex(@"export \* from ""[^""]*index"";");
    }
}
