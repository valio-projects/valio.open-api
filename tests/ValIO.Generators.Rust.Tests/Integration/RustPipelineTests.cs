using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.Rust.Tests.TestHelpers;

namespace ValIO.Generators.Rust.Tests.Integration;

/// <summary>
/// Integration tests for the full Rust pipeline.
/// </summary>
[TestFixture]
public class RustPipelineTests
{
    private RustPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new RustPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task SimpleSchema_ProducesAtLeastOneRustFile()
    {
        var doc = await RustPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().NotBeEmpty();
        ctx.GeneratedFiles.Select(f => f.RelativePath)
            .Should().Contain(p => p.EndsWith(".rs", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task EveryRustFile_HasNonEmptyContent()
    {
        var doc = await RustPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in RustFiles(ctx))
            f.Content.Should().NotBeNullOrWhiteSpace($"file {f.RelativePath} must have content");
    }

    [Test]
    public async Task EveryRustModelOrEnumFile_DerivesSerdeTraits()
    {
        var doc = await RustPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var modelOrEnumFiles = RustFiles(ctx)
            .Where(f => f.RelativePath.Replace('\\', '/').Contains("/src/models/"));

        modelOrEnumFiles.Should().NotBeEmpty();
        foreach (var f in modelOrEnumFiles)
        {
            f.Content.Should().Contain("#[derive(",
                $"{f.RelativePath} must derive standard traits");
            f.Content.Should().Contain("Serialize",
                $"{f.RelativePath} must derive Serialize");
            f.Content.Should().Contain("Deserialize",
                $"{f.RelativePath} must derive Deserialize");
        }
    }

    [Test]
    public async Task EveryRustFile_HasBalancedBraces()
    {
        var doc = await RustPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in RustFiles(ctx))
        {
            var stripped = Regex.Replace(f.Content, @"""([^""\\]|\\.)*""", "\"\"");
            var open = stripped.Count(c => c == '{');
            var close = stripped.Count(c => c == '}');
            open.Should().Be(close, $"file {f.RelativePath} has mismatched braces");
        }
    }

    [Test]
    public async Task ModelFile_ContainsPubStructUser()
    {
        var doc = await RustPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("user.rs", StringComparison.OrdinalIgnoreCase));
        userFile.Should().NotBeNull();
        userFile!.Content.Should().Contain("pub struct User");
    }

    [Test]
    public async Task ClientFile_UsesReqwestAndAsyncFn()
    {
        var doc = await RustPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').Contains("/src/api/", StringComparison.OrdinalIgnoreCase));
        clientFile.Should().NotBeNull("a client file must be generated");
        clientFile!.Content.Should().Contain("reqwest");
        clientFile.Content.Should().Contain("pub async fn");
        clientFile.Content.Should().Contain("Result<");
    }

    [Test]
    public async Task GeneratorRegistry_FindsRustByAlias()
    {
        await Task.CompletedTask;
        var registry = _harness.Services.GetService(typeof(IGeneratorRegistry)) as IGeneratorRegistry;
        registry.Should().NotBeNull();
        registry!.FindPlugin("rust").Should().NotBeNull();
        registry.FindPlugin("rs").Should().NotBeNull("alias rs should resolve to rust");
        registry.FindPlugin("RUST").Should().NotBeNull("case-insensitive");
        registry.FindPlugin("rust-reqwest").Should().NotBeNull("alias rust-reqwest");
    }

    private static IEnumerable<GeneratedFile> RustFiles(GenerationContext ctx) =>
        ctx.GeneratedFiles.Where(f =>
            f.RelativePath.EndsWith(".rs", StringComparison.OrdinalIgnoreCase));
}
