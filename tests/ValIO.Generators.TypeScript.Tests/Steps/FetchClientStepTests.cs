using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class FetchClientStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task EmptyTransports_NoFetchFilesEmitted()
    {
        // FetchClientStep only emits when "fetch" is explicitly listed
        // OR PluginOptions is null entirely. An explicit empty list
        // means "no transports".
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = [] });

        ctx.GeneratedFiles.Should().NotContain(f =>
            f.RelativePath.EndsWith(".fetch.ts", StringComparison.Ordinal));
    }

    [Test]
    public async Task FetchTransport_GeneratesFetchFiles()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        ctx.GeneratedFiles.Should().Contain(f =>
            f.RelativePath.EndsWith(".fetch.ts", StringComparison.Ordinal));
    }

    [Test]
    public async Task FetchClient_AsyncReturnTypeWrappedInPromise()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var client = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUsers.fetch.ts", StringComparison.Ordinal));
        client.Content.Should().Contain("async function");
        // Regression: previously emitted `: void` on async functions; now must be Promise<...>.
        client.Content.Should().Contain("Promise<");
        client.Content.Should().NotMatchRegex(@"async function \w+\([^)]*\):\s*void\s*\{");
    }

    [Test]
    public async Task FetchClient_QueryParams_BuiltViaUrlSearchParams()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var findByStatus = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("findPetsByStatus.fetch.ts", StringComparison.Ordinal));
        // Query string is now built explicitly with URLSearchParams (shared with the Next.js transport),
        // which handles encoding — no hand-rolled encodeURIComponent and no `name=undefined` for absent params.
        findByStatus.Content.Should().Contain("new URLSearchParams()");
        findByStatus.Content.Should().Contain("query.set(");
        findByStatus.Content.Should().Contain("\"status\"");
        findByStatus.Content.Should().Contain("(query.size > 0)");
        findByStatus.Content.Should().NotContain("encodeURIComponent");
    }

    [Test]
    public async Task FetchClient_OptionalQuerySource_UsesOptionalChaining()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        // updatePetWithForm has all-optional query params, so params.query itself is optional.
        // The generated URL builder must use `params.query?.x` to satisfy strict TS.
        var updateForm = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("updatePetWithForm.fetch.ts", StringComparison.Ordinal));
        updateForm.Content.Should().Contain("params.query?.");
    }

    [Test]
    public async Task FetchClient_HeadersAreCast_ToHeadersInit()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var deletePet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("deletePet.fetch.ts", StringComparison.Ordinal));
        deletePet.Content.Should().Contain("as HeadersInit");
    }
}
