using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.FSharp.Tests.TestHelpers;

namespace ValIO.Generators.FSharp.Tests.Integration;

/// <summary>
/// Integration tests for the full F# pipeline.
/// </summary>
[TestFixture]
public class FSharpPipelineTests
{
    private FSharpPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new FSharpPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task SimpleSchema_ProducesAtLeastOneFsFile()
    {
        var doc = await FSharpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().NotBeEmpty();
        ctx.GeneratedFiles.Select(f => f.RelativePath).Should().Contain(p => p.EndsWith(".fs"));
    }

    [Test]
    public async Task EveryGeneratedFile_IsNonEmpty()
    {
        var doc = await FSharpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles)
        {
            f.Content.Should().NotBeNullOrWhiteSpace($"file {f.RelativePath} must have content");
        }
    }

    [Test]
    public async Task EveryFsFile_StartsWithModuleDeclaration()
    {
        var doc = await FSharpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f =>
            f.RelativePath.EndsWith(".fs", StringComparison.OrdinalIgnoreCase)))
        {
            f.Content.Should().StartWith("module ", $"file {f.RelativePath} must start with `module `");
        }
    }

    [Test]
    public async Task ModelFile_ContainsRecordType()
    {
        var doc = await FSharpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("User.fs", StringComparison.OrdinalIgnoreCase));
        userFile.Should().NotBeNull();
        userFile!.Content.Should().Contain("type User =");
    }

    [Test]
    public async Task EnumFile_ContainsDiscriminatedUnion()
    {
        var doc = await FSharpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("UserRole.fs", StringComparison.OrdinalIgnoreCase));
        enumFile.Should().NotBeNull();
        enumFile!.Content.Should().Contain("type UserRole =");
        enumFile.Content.Should().Contain("|");
    }

    [Test]
    public async Task ClientFile_OpensRestSharpAndExecutes()
    {
        var doc = await FSharpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').StartsWith("api/", StringComparison.OrdinalIgnoreCase));
        clientFile.Should().NotBeNull();
        clientFile!.Content.Should().Contain("open RestSharp");
        clientFile.Content.Should().Contain("client.Execute");
    }

    [Test]
    public async Task GeneratorRegistry_FindsFSharpByAliases()
    {
        await Task.CompletedTask;
        var registry = _harness.Services.GetService(typeof(ValIO.Core.Plugins.IGeneratorRegistry))
            as ValIO.Core.Plugins.IGeneratorRegistry;
        registry.Should().NotBeNull();
        registry!.FindPlugin("fsharp").Should().NotBeNull();
        registry.FindPlugin("fs").Should().NotBeNull();
        registry.FindPlugin("f#").Should().NotBeNull();
        registry.FindPlugin("FSHARP").Should().NotBeNull("case-insensitive");
    }
}
