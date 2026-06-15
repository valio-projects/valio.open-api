using FluentAssertions;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Регрессия паттерна Swashbuckle <c>UseAllOfToExtendReferenceSchemas</c>
/// (<c>{allOf:[{$ref}],nullable:true}</c>) на всех TS-поверхностях: интерфейс-модели и
/// все валидаторы (zod3/zod4/valibot/typebox). До фикса zod падал
/// «Intersection must have at least two schemas», интерфейс молча терял <c>| null</c>.
/// </summary>
[TestFixture]
public class ValidatorNullableRefAllOfTests
{
    private PipelineTestHarness _harness = null!;
    private OpenApiDocument _document = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _harness = new PipelineTestHarness();
        _document = await PipelineTestHarness.LoadDocumentAsync("nullable-ref-allof.json");
    }

    [OneTimeTearDown]
    public void OneTimeTeardown() => _harness.Dispose();

    private static readonly string[] Validators = { "zod3", "zod4", "valibot", "typebox" };

    private async Task<string> SchemaAsync(string validator, string camelModel)
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = validator,
        });

        return ctx.GeneratedFiles
            .First(f => f.RelativePath.Replace('\\', '/')
                .EndsWith($"schemas/{camelModel}/{camelModel}.schema.ts", StringComparison.Ordinal))
            .Content;
    }

    [TestCaseSource(nameof(Validators))]
    public async Task NullableRefProperty_DoesNotThrow_AndIsNotSingleArmComposite(string validator)
    {
        var act = async () => await SchemaAsync(validator, "wrapper");
        var content = (await act.Should().NotThrowAsync()).Subject;

        // References the target schema; nullability expressed; never a degenerate 1-arm composite.
        content.Should().Contain("CompatibilityModeSchema");
        content.Should().MatchRegex("(?i)null", "nullable ref must render a null/nullish marker");

        var oneArm = validator switch
        {
            "zod3" or "zod4" => "z.intersection([",
            "valibot" => "intersect([",
            "typebox" => "Type.Intersect([",
            _ => "z.intersection(["
        };
        content.Should().NotContain(oneArm, "a single-element allOf must collapse, not emit a 1-arm intersection");
    }

    [TestCaseSource(nameof(Validators))]
    public async Task MultiElementAllOf_StillEmitsIntersection(string validator)
    {
        var content = await SchemaAsync(validator, "inherited");

        var intersectionIdiom = validator switch
        {
            "zod3" or "zod4" => "z.intersection(",
            "valibot" => "intersect(",
            "typebox" => "Type.Intersect(",
            _ => "z.intersection("
        };
        content.Should().Contain(intersectionIdiom,
            "a genuine 2-member allOf (inheritance) must still produce an intersection");
    }

    [Test]
    public async Task Zod3_NullableRef_EmitsNullableNotIntersection()
    {
        var content = await SchemaAsync("zod3", "wrapper");
        content.Should().Contain("CompatibilityModeSchema");
        content.Should().NotContain("z.intersection");
        content.Should().MatchRegex(@"\.null(able|ish)\(\)");
    }
}
