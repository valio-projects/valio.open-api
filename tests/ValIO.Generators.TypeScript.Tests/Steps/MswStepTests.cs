using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Unit tests for <c>MswStep</c> — emits one <c>mocks/handlers.ts</c> per generation
/// run when <c>options.MockLibrary == "msw"</c>.
/// </summary>
[TestFixture]
public class MswStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task NoMockLibrary_NoHandlersFile()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        ctx.GeneratedFiles.Should().NotContain(f =>
            f.RelativePath.EndsWith("mocks/handlers.ts", StringComparison.Ordinal));
    }

    [Test]
    public async Task MswMockLibrary_EmitsSingleHandlersFileWithVerbCalls()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            MockLibrary = "msw"
        });

        var handlerFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.EndsWith("mocks/handlers.ts", StringComparison.Ordinal))
            .ToList();
        handlerFiles.Should().HaveCount(1, "MswStep emits exactly one handlers.ts per run");

        var content = handlerFiles[0].Content;
        // Imports: http + HttpResponse from msw, plus the response model types.
        content.Should().Contain("import { http, HttpResponse } from \"msw\"");
        // Petstore has GET, POST, PUT, DELETE operations — verify lowercased verb dispatch.
        content.Should().Contain("http.get(");
        content.Should().Contain("http.post(");
        content.Should().Contain("export const handlers = [");
        content.Should().Contain("HttpResponse.json(");
    }

    [Test]
    public async Task MswMockLibrary_PathParamsConvertedToColonSyntax()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            MockLibrary = "msw"
        });

        var handlers = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("mocks/handlers.ts", StringComparison.Ordinal));

        // IR paths use {petId}; MSW http matchers use :petId. Both forms in the same
        // file would mean we missed a conversion.
        handlers.Content.Should().Contain(":petId");
        handlers.Content.Should().NotContain("{petId}");
    }
}
