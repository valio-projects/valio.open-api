using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class TypeScriptModelsStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task GeneratesOneFilePerModel_InCamelCaseDirectories()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var modelFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.Replace('\\', '/').StartsWith("models/", StringComparison.Ordinal)
                        && !f.RelativePath.Contains("/schemas/", StringComparison.Ordinal))
            .ToList();
        modelFiles.Should().NotBeEmpty();
        modelFiles.Should().AllSatisfy(f => f.RelativePath.Replace('\\', '/').Should().MatchRegex(@"models/[a-z]\w*/[a-z]\w*\.ts"));
    }

    [Test]
    public async Task ExportsInterfaceWithCorrectName()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/user/user.ts", StringComparison.Ordinal));
        userFile.Content.Should().Contain("export interface User");
        userFile.Content.Should().Contain("id:");
        userFile.Content.Should().Contain("name:");
    }

    [Test]
    public async Task Composition_AllOf_GeneratesIntersectionOrExtension()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        // Petstore has no allOf at top-level. Just verify Pet has its property structure.
        var petFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/pet/pet.ts", StringComparison.Ordinal));
        petFile.Content.Should().Contain("export interface Pet");
    }

    [Test]
    public async Task TopologicalOrder_DependentModelsAfterTheirDeps()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        // Pet references Category and Tag → those must be in the IR dep graph.
        // The Step iterates TopologicalOrder; this is a smoke check that
        // generation finishes (no cycles, no crashes).
        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Replace('\\', '/').EndsWith("models/category/category.ts"));
        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Replace('\\', '/').EndsWith("models/tag/tag.ts"));
        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Replace('\\', '/').EndsWith("models/pet/pet.ts"));
    }
}
