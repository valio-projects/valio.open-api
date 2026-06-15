using FluentAssertions;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Тело ответа с union-типом (oneOf, напр. <c>ProblemDetails | HttpValidationProblemDetails</c>)
/// должно ВАЛИДИРОВАТЬСЯ схемой, построенной из типа ответа, выбранной библиотекой —
/// а не возвращаться сырым. Регрессия: раньше per-status ветка для union отдавала <c>response.data</c>
/// без схемы.
/// </summary>
[TestFixture]
public class UnionResponseValidationTests
{
    private PipelineTestHarness _harness = null!;
    private OpenApiDocument _document = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _harness = new PipelineTestHarness();
        _document = await PipelineTestHarness.LoadDocumentAsync("status-union-response.json");
    }

    [OneTimeTearDown]
    public void OneTimeTeardown() => _harness.Dispose();

    private async Task<string> ClientAsync(string validation)
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["axios"],
            ValidationLibrary = validation,
        });

        return ctx.GeneratedFiles
            .First(f => f.RelativePath.EndsWith("getThing.axios.ts", StringComparison.Ordinal))
            .Content;
    }

    [TestCase("zod3")]
    [TestCase("zod4")]
    public async Task Zod_UnionResponse_ValidatedWithZodUnion(string validation)
    {
        var content = await ClientAsync(validation);
        // 400 union → z.union([...]).parse(...), members imported.
        content.Should().Contain("z.union([ProblemDetailsSchema, HttpValidationProblemDetailsSchema]).parse");
        content.Should().Contain("import { HttpValidationProblemDetailsSchema }");
        content.Should().Contain("import { ProblemDetailsSchema }");
        // 200 model ref still validated.
        content.Should().Contain("ThingSchema.parse(response.data)");
    }

    [Test]
    public async Task Valibot_UnionResponse_ValidatedWithVParse()
    {
        var content = await ClientAsync("valibot");
        content.Should().Contain("import * as v from \"valibot\"");
        content.Should().Contain("v.union([ProblemDetailsSchema, HttpValidationProblemDetailsSchema])");
        content.Should().Contain("v.parse(");
        // The 400 union body must not be raw response.data.
        content.Should().NotContain("status: 400, body: response.data");
    }

    [Test]
    public async Task TypeBox_UnionResponse_ValidatedWithValueParse()
    {
        var content = await ClientAsync("typebox");
        content.Should().Contain("@sinclair/typebox/value");
        content.Should().Contain("Type.Union([ProblemDetailsSchema, HttpValidationProblemDetailsSchema])");
        content.Should().Contain("Value.Parse(");
        content.Should().NotContain("status: 400, body: response.data");
    }
}
