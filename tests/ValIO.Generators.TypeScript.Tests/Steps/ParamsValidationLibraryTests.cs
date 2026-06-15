using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Route/query параметры валидируются выбранной библиотекой: zod — по импортируемой именованной
/// схеме (<c>{op}RouteParamsSchema</c>), valibot/typebox — по inline-схеме параметров.
/// Раньше для не-zod параметры не валидировались вовсе (жёсткая привязка к zod).
/// </summary>
[TestFixture]
public class ParamsValidationLibraryTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    private async Task<string> GetPetByIdClientAsync(string validation)
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["axios"],
            ValidationLibrary = validation,
        });

        return ctx.GeneratedFiles
            .First(f => f.RelativePath.EndsWith("getPetById.axios.ts", StringComparison.Ordinal))
            .Content;
    }

    [TestCase("zod3")]
    [TestCase("zod4")]
    public async Task Zod_RouteParams_UseImportedNamedSchema(string validation)
    {
        var content = await GetPetByIdClientAsync(validation);
        content.Should().Contain("GetPetByIdRouteParamsSchema.parse");
        content.Should().Contain(".params.schema");
        content.Should().NotContain("v.parse(");
        content.Should().NotContain("Value.Parse(");
    }

    [Test]
    public async Task Valibot_RouteParams_BuiltInlineWithVObject()
    {
        var content = await GetPetByIdClientAsync("valibot");
        content.Should().Contain("import * as v from \"valibot\"");
        content.Should().MatchRegex(@"v\.parse\(\s*v\.object\(");
        // No dependency on a separate (zod-only) params schema file.
        content.Should().NotContain("GetPetByIdRouteParamsSchema");
    }

    [Test]
    public async Task TypeBox_RouteParams_BuiltInlineWithTypeObject()
    {
        var content = await GetPetByIdClientAsync("typebox");
        content.Should().Contain("@sinclair/typebox");
        content.Should().MatchRegex(@"Value\.Parse\(\s*Type\.Object\(");
        content.Should().NotContain("GetPetByIdRouteParamsSchema");
    }
}
