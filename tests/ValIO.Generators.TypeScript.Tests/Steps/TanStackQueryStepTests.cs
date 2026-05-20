using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class TanStackQueryStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task NoQueryWrapper_NoQueryFilesEmitted()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        ctx.GeneratedFiles.Should().NotContain(f =>
            f.RelativePath.EndsWith(".query.ts", StringComparison.Ordinal));
    }

    [Test]
    public async Task TanStackReact_GeneratesUseQueryAndQueryKey()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-react",
        });

        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("@tanstack/react-query");
        query.Content.Should().Contain("useQuery");
        query.Content.Should().Contain("getGetUsersQueryKey");
    }

    [Test]
    public async Task TanStackReact_PostMethod_GeneratesUseMutation()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-react",
        });

        var addPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("addPet.query.ts", StringComparison.Ordinal));
        addPet.Content.Should().Contain("useMutation");
        addPet.Content.Should().Contain("useAddPet");
    }

    [Test]
    public async Task TanStackReact_ClientImport_MatchesPrimaryTransport()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["axios"],
            QueryWrapper = "tanstack-react",
        });

        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("./getUsers.axios");
        query.Content.Should().NotContain("./getUsers.fetch");
    }
}
