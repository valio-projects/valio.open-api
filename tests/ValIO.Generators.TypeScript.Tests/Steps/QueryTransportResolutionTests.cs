using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Tests the <c>queryTransport</c> option and the transport-resolution/auto-fix step: the query hooks wrap the
/// resolved client transport, the transport is auto-added (with a non-blocking warning surfaced via
/// <c>GenerationContext.Warnings</c>) when it would otherwise be missing.
/// </summary>
[TestFixture]
public class QueryTransportResolutionTests
{
    /// <summary>The shared pipeline harness.</summary>
    private PipelineTestHarness _harness = null!;

    /// <summary>Creates the harness once for the fixture.</summary>
    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    /// <summary>Disposes the harness after the fixture.</summary>
    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    // Wrapper selected but no client-capable transport → auto-add fetch + warn; hooks wrap the emitted fetch client.
    [Test]
    public async Task WrapperWithoutClientTransport_AutoAddsFetch_AndWarns()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["nextjs"],
            QueryWrapper = "tanstack-react",
        });

        // The fetch client is auto-emitted so the hook import resolves…
        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.EndsWith("getUsers.fetch.ts", StringComparison.Ordinal));
        var query = ctx.GeneratedFiles.First(f => f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("./getUsers.fetch");
        // …and a non-blocking warning explains the adjustment.
        ctx.Warnings.Should().Contain(w => w.Contains("no client-capable transport", StringComparison.OrdinalIgnoreCase));
    }

    // Explicit queryTransport is honoured by the hooks and auto-added to transports when absent (with a warning).
    [Test]
    public async Task ExplicitQueryTransport_IsHonoured_AndAutoAdded()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["nextjs"],
            QueryWrapper = "tanstack-react",
            QueryTransport = "axios",
        });

        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.EndsWith("getUsers.axios.ts", StringComparison.Ordinal));
        var query = ctx.GeneratedFiles.First(f => f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("./getUsers.axios");
        query.Content.Should().NotContain("./getUsers.fetch");
        ctx.Warnings.Should().Contain(w => w.Contains("queryTransport 'axios'", StringComparison.Ordinal));
    }

    // queryTransport already present in transports → honoured, no auto-add, no warning.
    [Test]
    public async Task QueryTransport_AlreadySelected_NoWarning()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["axios", "nextjs"],
            QueryWrapper = "tanstack-react",
            QueryTransport = "axios",
        });

        var query = ctx.GeneratedFiles.First(f => f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("./getUsers.axios");
        ctx.Warnings.Should().BeEmpty();
    }
}
