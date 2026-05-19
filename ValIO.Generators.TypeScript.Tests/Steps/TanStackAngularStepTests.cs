using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class TanStackAngularStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task NotActivated_NoAngularQueryFiles()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-react", // not angular
        });

        ctx.GeneratedFiles.Should().NotContain(f =>
            f.Content.Contains("@tanstack/angular-query-experimental", StringComparison.Ordinal));
    }

    [Test]
    public async Task TanStackAngular_GeneratesInjectQueryHook()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-angular",
        });

        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("@tanstack/angular-query-experimental");
        query.Content.Should().Contain("injectQuery");
        query.Content.Should().Contain("getGetUsersQueryKey");
        // No explicit ReturnType annotation — TS infers from injectQuery<T>(...).
        query.Content.Should().NotContain("ReturnType<typeof injectQuery>");
        // Safe spread of options.
        query.Content.Should().Contain("options ?? {}");
    }

    [Test]
    public async Task TanStackAngular_PostOperation_GeneratesInjectMutation()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-angular",
        });

        var addPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("addPet.query.ts", StringComparison.Ordinal));
        addPet.Content.Should().Contain("injectMutation");
        addPet.Content.Should().Contain("useAddPet");
    }

    [Test]
    public async Task TanStackAngular_ClientImport_MatchesPrimaryTransport()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["axios"],
            QueryWrapper = "tanstack-angular",
        });

        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("./getUsers.axios");
        query.Content.Should().NotContain("./getUsers.fetch");
    }
}
