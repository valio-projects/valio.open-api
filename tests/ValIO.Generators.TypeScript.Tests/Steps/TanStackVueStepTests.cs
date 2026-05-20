using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class TanStackVueStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task TanStackVue_GeneratesVueQueryHooks()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-vue",
        });

        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUserById.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("@tanstack/vue-query");
        query.Content.Should().Contain("MaybeRef");
        query.Content.Should().Contain("computed");
        query.Content.Should().Contain("unref");
    }

    [Test]
    public async Task TanStackVue_DoesNotAnnotateExplicitReturnType()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["ky"],
            QueryWrapper = "tanstack-vue",
        });

        // Regression: previously `ReturnType<typeof useQuery>` resolved to
        // UseQueryReturnType<unknown,...> and broke type inference.
        // The fix removes the explicit return annotation entirely from
        // every generated hook.
        var queryFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.EndsWith(".query.ts", StringComparison.Ordinal))
            .ToList();
        queryFiles.Should().NotBeEmpty();
        queryFiles.Should().AllSatisfy(f =>
        {
            f.Content.Should().NotContain("ReturnType<typeof useQuery>");
            f.Content.Should().NotContain("ReturnType<typeof useMutation>");
        });
    }

    [Test]
    public async Task TanStackVue_Options_TypedAsObjectNotUnknown()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["ky"],
            QueryWrapper = "tanstack-vue",
        });

        var addPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("addPet.query.ts", StringComparison.Ordinal));
        // options should be `options?: object`, not `options?: unknown`.
        addPet.Content.Should().Contain("options?: object");
        addPet.Content.Should().NotContain("options?: unknown");
        // spread must guard against undefined for object spread to be legal.
        addPet.Content.Should().Contain("options ?? {}");
    }

    [Test]
    public async Task TanStackVue_ClientImport_MatchesPrimaryTransport()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["ky"],
            QueryWrapper = "tanstack-vue",
        });

        var addPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("addPet.query.ts", StringComparison.Ordinal));
        addPet.Content.Should().Contain("./addPet.ky");
        addPet.Content.Should().NotContain("./addPet.fetch");
    }
}
