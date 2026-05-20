using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class ZodSchemasStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task Zod3_UsesPlainZodImport()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        var schema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".schema.ts", StringComparison.Ordinal));
        schema.Content.Should().Contain("from 'zod'");
        schema.Content.Should().NotContain("from 'zod/v4'");
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

        var schema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".schema.ts", StringComparison.Ordinal));
        schema.Content.Should().Contain("from 'zod/v4'");
    }

    [Test]
    public async Task ModelSchema_ImportsModelType_ViaRelativeUpwardsPath()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        var userSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("schemas/user/user.schema.ts", StringComparison.Ordinal));
        // From models/schemas/user/user.schema.ts, the model lives at
        // models/user/user.ts → relative ../../user/user.
        userSchema.Content.Should().Contain("../../user/user");
    }

    [Test]
    public async Task DependencyImports_AreCamelCased()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        // Pet schema imports CategorySchema and TagSchema from sibling directories.
        var petSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("schemas/pet/pet.schema.ts", StringComparison.Ordinal));
        petSchema.Content.Should().Contain("'../category/category.schema'");
        petSchema.Content.Should().Contain("'../tag/tag.schema'");
    }
}
