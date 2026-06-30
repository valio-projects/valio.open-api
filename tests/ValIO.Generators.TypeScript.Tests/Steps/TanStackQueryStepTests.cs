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

    // BUG #2: under verbatimModuleSyntax the @tanstack/react-query type symbols must be `import type`.
    [Test]
    public async Task TanStackReact_TanStackTypeSymbols_AreImportedAsTypeOnly()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-react",
        });

        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUsers.query.ts", StringComparison.Ordinal));

        // Runtime hooks stay a value import...
        query.Content.Should().Contain("import { useInfiniteQuery, useQuery, useIsFetching } from \"@tanstack/react-query\";");
        // ...type-only symbols use `import type` (otherwise TS1484).
        query.Content.Should().Contain("import type { UseQueryOptions } from \"@tanstack/react-query\";");
        query.Content.Should().Contain("import type { QueryClient } from \"@tanstack/react-query\";");
        // No type symbol leaks into the value import.
        query.Content.Should().NotContain("import { useInfiniteQuery, useQuery, useIsFetching, QueryClient");
    }

    // BUG #3: a parameterless mutation must not forward the `void` variables into the client's options slot.
    [Test]
    public async Task TanStackReact_ParameterlessMutation_DoesNotForwardVoidVariables()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("parameterless-mutation.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-react",
        });

        var mutation = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".query.ts", StringComparison.Ordinal));

        mutation.Content.Should().Contain("useMutation");
        // The placeholder is `_variables` (underscored → no noUnusedParameters error)...
        mutation.Content.Should().Contain("_variables: void");
        mutation.Content.Should().NotContain("(variables: void");
        // ...and it is NOT forwarded to the client function (which would land `void` in the options slot).
        mutation.Content.Should().NotContain("variables, requestOptions");
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
