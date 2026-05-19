using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class TanStackSvelteStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task TanStackSvelte_GeneratesCreateQueryHookWithAccessor()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-svelte",
        });

        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("@tanstack/svelte-query");
        query.Content.Should().Contain("createQuery");
        // Svelte 5 + strict tsc requires the accessor pattern even though
        // older Svelte allowed plain objects.
        // createQuery<TResult>(() => ({ ... }))
        query.Content.Should().MatchRegex(@"createQuery(<[^>]+>)?\(\(\)\s*=>");
        query.Content.Should().NotContain("ReturnType<typeof createQuery>");
    }

    [Test]
    public async Task TanStackSvelte_PostOperation_GeneratesCreateMutation()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-svelte",
        });

        var addPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("addPet.query.ts", StringComparison.Ordinal));
        addPet.Content.Should().Contain("createMutation");
        addPet.Content.Should().Contain("options ?? {}");
    }

    [Test]
    public async Task TanStackSvelte_ClientImport_MatchesPrimaryTransport()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["axios"],
            QueryWrapper = "tanstack-svelte",
        });

        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("./getUsers.axios");
    }
}
