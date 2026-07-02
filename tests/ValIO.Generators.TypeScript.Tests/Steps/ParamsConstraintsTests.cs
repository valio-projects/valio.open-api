using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Tests that route/query parameter validation constraints (lengths, ranges, patterns) are emitted as an exported
/// constants object in <c>{op}.params.ts</c> (mirroring model-property <c>{Model}Constraints</c>) and applied in
/// <c>{op}.params.schema.ts</c> — by reference for Zod (imported from <c>./{op}.params</c>) and inline for
/// Valibot/TypeBox (matching how each library treats model-property constraints).
/// </summary>
[TestFixture]
public class ParamsConstraintsTests
{
    /// <summary>The shared pipeline harness.</summary>
    private PipelineTestHarness _harness = null!;

    /// <summary>Creates the harness once for the fixture.</summary>
    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    /// <summary>Disposes the harness after the fixture.</summary>
    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    // The params interface file carries the exported `as const` constants object regardless of validation lib.
    [Test]
    public async Task ParamsInterface_EmitsExportedConstraintConstants()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("constrained-params.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"], ValidationLibrary = "zod4" });

        var paramsFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getItems.params.ts", StringComparison.Ordinal));

        paramsFile.Content.Should().Contain("export const GetItemsRouteParamsConstraints");
        paramsFile.Content.Should().Contain("export const GetItemsQueryParamsConstraints");
        paramsFile.Content.Should().Contain("minLength: 3");
        paramsFile.Content.Should().Contain("pattern: \"^[A-Z]{3}$\"");
        paramsFile.Content.Should().Contain("min: 1");
        paramsFile.Content.Should().Contain("max: 100");
        paramsFile.Content.Should().Contain("as const");
    }

    // Zod references the imported constants object (values live in {op}.params.ts, imported into the schema).
    [Test]
    public async Task ZodParamsSchema_ReferencesImportedConstraintConstants()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("constrained-params.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"], ValidationLibrary = "zod4" });

        var schema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getItems.params.schema.ts", StringComparison.Ordinal));

        // Constants are imported (value import, not type-only) from the sibling params interface file.
        schema.Content.Should().Contain("import { GetItemsRouteParamsConstraints, GetItemsQueryParamsConstraints } from \"./getItems.params\"");
        // …and referenced in the schema rather than inlining magic numbers.
        schema.Content.Should().Contain(".min(GetItemsQueryParamsConstraints.limit.min)");
        schema.Content.Should().Contain(".max(GetItemsQueryParamsConstraints.limit.max)");
        schema.Content.Should().Contain(".min(GetItemsRouteParamsConstraints.code.minLength)");
    }

    // Valibot inlines literal rule values (like its model schemas) — no constants import.
    [Test]
    public async Task ValibotParamsSchema_InlinesLiteralConstraints()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("constrained-params.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"], ValidationLibrary = "valibot" });

        var schema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getItems.params.schema.ts", StringComparison.Ordinal));

        schema.Content.Should().Contain("v.minValue(1)");
        schema.Content.Should().Contain("v.maxValue(100)");
        schema.Content.Should().Contain("v.minLength(2)");
        schema.Content.Should().NotContain("Constraints");
    }

    // TypeBox inlines rule values into the schema options (like its model schemas) — no constants import.
    [Test]
    public async Task TypeBoxParamsSchema_InlinesConstraintOptions()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("constrained-params.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"], ValidationLibrary = "typebox" });

        var schema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getItems.params.schema.ts", StringComparison.Ordinal));

        schema.Content.Should().Contain("Type.Integer({ minimum: 1, maximum: 100 })");
        schema.Content.Should().Contain("minLength: 2, maxLength: 50");
        schema.Content.Should().NotContain("Constraints");
    }
}
