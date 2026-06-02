using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class TypeBoxSchemasStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task NotEnabled_NoTypeBoxSchemas()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        ctx.GeneratedFiles.Should().NotContain(f => f.Content.Contains("@sinclair/typebox", StringComparison.Ordinal));
    }

    [Test]
    public async Task ModelSchema_UsesTypeObject_AndStaticInfer()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "typebox",
        });

        var userSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("schemas/user/user.schema", StringComparison.OrdinalIgnoreCase));
        userSchema.Content.Should().Contain("@sinclair/typebox");
        userSchema.Content.Should().Contain("Type.Object");
        userSchema.Content.Should().Contain("export const UserSchema");
        userSchema.Content.Should().Contain("Static<typeof UserSchema>");
    }

    [Test]
    public async Task DependencySchema_ImportPathUsesCamelCase()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "typebox",
        });

        var petSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("schemas/pet/pet.schema", StringComparison.OrdinalIgnoreCase));
        petSchema.Content.Should().NotMatchRegex(@"\.\./(Category|Tag)/(Category|Tag)\.schema");
    }

    [Test]
    public async Task EnumSchema_ReusesModelValues_ViaUnionMap()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "typebox",
        });

        var enumSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("schemas/userRole/userRole.schema.ts", StringComparison.Ordinal));

        // Reuses UserRoleValues from the enum model: Type.Union(UserRoleValues.map((value) => Type.Literal(value))).
        enumSchema.Content.Should().Contain("import { UserRoleValues } from \"../../models/userRole/userRole\"");
        enumSchema.Content.Should().Contain("Type.Union(UserRoleValues.map(");
        enumSchema.Content.Should().Contain("Type.Literal(value)");
    }
}
