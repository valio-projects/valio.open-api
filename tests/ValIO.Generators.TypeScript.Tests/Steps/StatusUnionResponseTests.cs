using FluentAssertions;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Per-status result-ветки клиента должны генерироваться для ЛЮБОГО типизированного ответа,
/// включая union (oneOf/anyOf) — например, полиморфные ошибки <c>ProblemDetails | HttpValidationProblemDetails</c>
/// от UseOneOfForPolymorphism. Раньше axios/fetch фильтровали только IrModelReference и
/// молча теряли обработку таких статус-кодов (Got/Ky/OFetch/Undici делали корректно).
/// </summary>
[TestFixture]
public class StatusUnionResponseTests
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

    private async Task<string> ClientAsync(string transport, string suffix)
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = [transport],
            ValidationLibrary = "zod3",
        });

        return ctx.GeneratedFiles
            .First(f => f.RelativePath.EndsWith($"getThing{suffix}", StringComparison.Ordinal))
            .Content;
    }

    [TestCase("axios", ".axios.ts")]
    [TestCase("fetch", ".fetch.ts")]
    public async Task UnionResponse_EmitsStatusBlock_ForAllDeclaredCodes(string transport, string suffix)
    {
        var content = await ClientAsync(transport, suffix);

        // 400 is a oneOf union — the block must still be generated (was dropped before the fix).
        content.Should().MatchRegex(@"status\s*===\s*400");
        // 404 is a plain $ref — schema-parsed body.
        content.Should().MatchRegex(@"status\s*===\s*404");
        content.Should().Contain("ProblemDetailsSchema.parse");
    }
}
