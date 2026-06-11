using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Regression tests for route-parameter interpolation when the OpenAPI path
/// template placeholder casing differs from the parameter's declared name
/// (e.g. path <c>/api/dictionaries/{dictionaryId}/items</c> with parameter
/// <c>name: DictionaryId</c> — common in ASP.NET-generated specs).
///
/// Previously every transport matched the <c>{placeholder}</c> against the path
/// case-sensitively (<see cref="System.StringComparison.Ordinal"/>) using the
/// parameter name, so the lookup failed and the literal <c>{dictionaryId}</c>
/// survived in the URL → backend 404. The match is now case-insensitive.
/// </summary>
[TestFixture]
public class RouteParamCasingTests
{
    // ${<source>.DictionaryId} — interpolation present, regardless of source
    // (parsedRoute / params / params.route).
    private const string InterpolatedFieldPattern = @"\$\{[^}]*DictionaryId\}";

    // The bug signature: the camelCase path token left un-substituted.
    private const string LiteralPlaceholder = "{dictionaryId}";

    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [TestCase("axios", "getDictionaryItemsList.axios.ts")]
    [TestCase("fetch", "getDictionaryItemsList.fetch.ts")]
    [TestCase("ky", "getDictionaryItemsList.ky.ts")]
    [TestCase("got", "getDictionaryItemsList.got.ts")]
    [TestCase("undici", "getDictionaryItemsList.undici.ts")]
    [TestCase("ofetch", "getDictionaryItemsList.ofetch.ts")]
    public async Task Transport_InterpolatesRouteParam_DespiteCasingMismatch(string transport, string fileSuffix)
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("route-param-casing.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = [transport] });

        var client = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(fileSuffix, StringComparison.Ordinal));

        client.Content.Should().NotContain(LiteralPlaceholder,
            "the camelCase path placeholder must be interpolated, not left literal");
        client.Content.Should().MatchRegex(InterpolatedFieldPattern,
            "the URL must reference the (PascalCase) route field DictionaryId");
    }

    [Test]
    public async Task Axios_WithZod_InterpolatesParsedRouteField()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("route-param-casing.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["axios"],
            ValidationLibrary = "zod3",
        });

        var client = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getDictionaryItemsList.axios.ts", StringComparison.Ordinal));

        // Mirrors the user-reported target output exactly.
        client.Content.Should().Contain("parsedRoute.DictionaryId");
        client.Content.Should().NotContain(LiteralPlaceholder);
    }

    [Test]
    public async Task AngularRxjsService_InterpolatesRouteParam_DespiteCasingMismatch()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("route-param-casing.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["angular-rxjs"] });

        var service = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".service.ts", StringComparison.Ordinal));

        service.Content.Should().NotContain(LiteralPlaceholder);
        service.Content.Should().MatchRegex(InterpolatedFieldPattern);
    }
}
