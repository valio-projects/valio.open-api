using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class TanStackSolidStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task TanStackSolid_GeneratesCreateQueryHookWithGetter()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-solid",
        });

        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("@tanstack/solid-query");
        query.Content.Should().Contain("createQuery");
        // Getter wrapping: createQuery(() => ({ ... }))
        // createQuery<TResult>(() => ({ ... }))
        query.Content.Should().MatchRegex(@"createQuery(<[^>]+>)?\(\(\)\s*=>");
        query.Content.Should().NotContain("ReturnType<typeof createQuery>");
        query.Content.Should().Contain("options ?? {}");
    }

    [Test]
    public async Task TanStackSolid_PostOperation_GeneratesCreateMutation()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-solid",
        });

        var addPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("addPet.query.ts", StringComparison.Ordinal));
        addPet.Content.Should().Contain("createMutation");
    }

    [Test]
    public async Task TanStackSolid_ClientImport_MatchesPrimaryTransport()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["ky"],
            QueryWrapper = "tanstack-solid",
        });

        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("./getUsers.ky");
    }
}
