using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class TypeScriptEnumsStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task DefaultFormat_GeneratesEnumFile()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var enumFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/userRole/userRole.ts", StringComparison.Ordinal));
        enumFile.Should().NotBeNull("UserRole enum file must be generated");
    }

    [Test]
    public async Task UnionFormat_GeneratesStringUnionType()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            EnumFormat = "union",
        });

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("userRole/userRole.ts", StringComparison.Ordinal));
        enumFile.Content.Should().Contain("\"admin\"");
        enumFile.Content.Should().Contain("\"user\"");
        enumFile.Content.Should().Contain("\"guest\"");
    }

    [Test]
    public async Task ValuesArray_IsLiteralTuple_NotWidenedToReadonlyStringArray()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("userRole/userRole.ts", StringComparison.Ordinal));

        // Values keep their literal-tuple type via `as const satisfies` so they can be reused
        // by z.enum(...) / v.picklist(...) / Type.Union(...). The widening `: readonly string[]`
        // annotation would defeat that and must NOT be emitted.
        enumFile.Content.Should().Contain("export const UserRoleValues = [");
        enumFile.Content.Should().Contain("as const satisfies readonly UserRole[]");
        enumFile.Content.Should().NotContain("UserRoleValues: readonly string[]");
    }
}
