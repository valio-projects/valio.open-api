using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class ValibotSchemasStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task NotEnabled_NoSchemasGenerated()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        ctx.GeneratedFiles.Should().NotContain(f => f.Content.Contains("import * as v from \"valibot\"", StringComparison.Ordinal));
    }

    [Test]
    public async Task ModelSchema_UsesValibotObject()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "valibot",
        });

        var userSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("models/user/user.schema", StringComparison.OrdinalIgnoreCase));
        userSchema.Content.Should().Contain("import * as v from \"valibot\"");
        userSchema.Content.Should().Contain("v.object");
        userSchema.Content.Should().Contain("export const UserSchema");
        userSchema.Content.Should().Contain("v.InferOutput<typeof UserSchema>");
    }

    [Test]
    public async Task DependencySchema_ImportPathUsesCamelCase()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "valibot",
        });

        // pet schema references Category and Tag — those imports must use
        // camelCased file names (./category/category.schema), not PascalCase.
        var petSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("models/pet/pet.schema", StringComparison.OrdinalIgnoreCase));
        petSchema.Content.Should().Contain("../category/category.schema");
        petSchema.Content.Should().Contain("../tag/tag.schema");
        petSchema.Content.Should().NotMatchRegex(@"\.\./(Category|Tag)/(Category|Tag)\.schema");
    }

    [Test]
    public async Task EnumSchema_GeneratesPickListWithValuesArray()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "valibot",
        });

        var userRoleSchema = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').Contains("models/userRole/userRole.schema", StringComparison.OrdinalIgnoreCase));
        userRoleSchema.Should().NotBeNull();
        userRoleSchema!.Content.Should().Contain("v.picklist(UserRoleValues)");
        // Values are imported from the enum model, not re-declared in the schema file.
        userRoleSchema.Content.Should().Contain("import { UserRoleValues } from \"./userRole\"");
        userRoleSchema.Content.Should().NotContain("UserRoleValues = [");
    }
}
