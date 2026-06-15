using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>
/// Сквозная регрессия на реальных документах Swashbuckle (ASP.NET, OpenAPI 3.0.1, ~319 схем)
/// во всех комбинациях настроек UseAllOfForInheritance / UseAllOfToExtendReferenceSchemas /
/// UseOneOfForPolymorphism, прогоняемых через ВСЕ TS-поверхности (интерфейсы + zod3/zod4/valibot/typebox).
/// Подтверждает отсутствие исключения «Intersection must have at least two schemas» и генерацию файлов.
/// Помечено категорией Regression — тяжёлые фикстуры исключаются из быстрого цикла.
/// </summary>
[TestFixture]
[Category("Regression")]
public class SwaggerVariantsRegressionTests
{
    private static readonly string[] Variants =
    {
        "variant-inheritance.json",
        "variant-extendref.json",
        "variant-inheritance-extendref.json",
        "variant-inheritance-polymorphism.json",
        "variant-extendref-polymorphism.json",
        "variant-inheritance-extendref-polymorphism.json",
    };

    private static readonly string[] Validators = { "zod3", "zod4", "valibot", "typebox" };

    private static IEnumerable<TestCaseData> Cases()
    {
        foreach (var variant in Variants)
            foreach (var validator in Validators)
                yield return new TestCaseData(variant, validator).SetName($"{variant} [{validator}]");
    }

    [TestCaseSource(nameof(Cases))]
    public async Task FullPipeline_CompletesWithoutException_AndEmitsFiles(string variant, string validator)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentAsync(variant);

        GenerationContext ctx = null!;
        var run = async () => ctx = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = validator,
        });

        await run.Should().NotThrowAsync(
            "single-element allOf (UseAllOfToExtendReferenceSchemas) must not crash any TS surface");
        ctx.GeneratedFiles.Should().NotBeEmpty();
        ctx.GeneratedFiles.Count.Should().BeGreaterThan(100, "~319 schemas produce many model + schema files");

        // No surface should emit a degenerate single-element intersection idiom.
        foreach (var file in ctx.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".ts", StringComparison.Ordinal)))
        {
            file.Content.Should().NotContain("z.intersection([\n", "no 1-arm Zod intersection");
        }
    }
}
