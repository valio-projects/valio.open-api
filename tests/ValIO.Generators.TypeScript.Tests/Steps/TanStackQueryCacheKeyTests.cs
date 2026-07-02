using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Tests for the shared cache-identity module (<c>{op}.query-keys.ts</c>) and the two bug fixes it enables:
/// BUG #10 (query keys inline the real path-parameter value + carry the query filter as an object tail, never the
/// literal <c>"{id}"</c> placeholder) and BUG #11 (the React option factories merge the caller's
/// <c>options.query</c> / <c>options.mutation</c> instead of dropping them). Also covers the hybrid
/// Next.js + axios + TanStack scenario that shares one cache identity across server and client.
/// </summary>
[TestFixture]
public class TanStackQueryCacheKeyTests
{
    /// <summary>The shared pipeline harness.</summary>
    private PipelineTestHarness _harness = null!;

    /// <summary>Creates the harness once for the fixture.</summary>
    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    /// <summary>Disposes the harness after the fixture.</summary>
    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    // BUG #10 — flat route-only op: the key inlines params.id (not the "{id}" placeholder, not the whole params).
    [Test]
    public async Task QueryKey_FlatRouteParam_InlinesParamValue()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("apartments.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-react",
        });

        var keys = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getApartment.query-keys.ts", StringComparison.Ordinal));

        keys.Content.Should().Contain("export const getGetApartmentQueryKey");
        keys.Content.Should().Contain("\"api\"");
        keys.Content.Should().Contain("\"apartments\"");
        keys.Content.Should().Contain("params.id");
        // The literal placeholder must never appear, and flat mode must not wrap in params.route.
        keys.Content.Should().NotContain("\"{id}\"");
        keys.Content.Should().NotContain("params.route");
    }

    // BUG #10 — aggregate route+query op: id inlined at params.route.id, query as a conditional object tail.
    [Test]
    public async Task QueryKey_RouteAndQuery_InlinesRouteIdAndAppendsQueryObjectTail()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("apartments.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-react",
        });

        var keys = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getApartmentInvestor.query-keys.ts", StringComparison.Ordinal));

        keys.Content.Should().Contain("export const getGetApartmentInvestorQueryKey");
        keys.Content.Should().Contain("params.route.id");
        keys.Content.Should().Contain("\"investor\"");
        // Optional query filter → conditional object tail (supports partial-match invalidation).
        keys.Content.Should().Contain("...(params.query ? [params.query] : [])");
        keys.Content.Should().NotContain("\"{id}\"");
        // The infinite key variant is emitted too, discriminated by an "infinite" segment.
        keys.Content.Should().Contain("export const getGetApartmentInvestorInfiniteQueryKey");
        keys.Content.Should().Contain("\"infinite\"");
    }

    // BUG #11 — the query option factories spread the caller's options.query before the fixed queryKey/queryFn.
    [Test]
    public async Task QueryOptions_MergesCallerQueryOptions()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("apartments.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-react",
        });

        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getApartmentInvestor.query.ts", StringComparison.Ordinal));

        // Both the plain and the infinite option factories must forward the caller's options (enabled,
        // placeholderData, retry, staleTime, select, …) rather than dropping everything but queryKey.
        query.Content.Should().Contain("...options?.query");
        // Imports the shared key factory instead of declaring it inline.
        query.Content.Should().Contain("from \"./getApartmentInvestor.query-keys\"");
        query.Content.Should().Contain("getGetApartmentInvestorQueryKey");
    }

    // BUG #11 (mutation) — the mutation option factory spreads options.mutation and the result is UseMutationOptions.
    [Test]
    public async Task MutationOptions_MergesCallerMutationOptions()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-react",
        });

        var addPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("addPet.query.ts", StringComparison.Ordinal));

        addPet.Content.Should().Contain("...options?.mutation");
        // The options result must be the full UseMutationOptions so onSuccess/onError/retry/… type-check.
        addPet.Content.Should().Contain("GetAddPetMutationOptionsResult = UseMutationOptions");
    }

    // A mutation is a write, so no shared cache-keys module is emitted for it.
    [Test]
    public async Task Mutation_NoQueryKeysModuleEmitted()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            QueryWrapper = "tanstack-react",
        });

        ctx.GeneratedFiles.Should().NotContain(f =>
            f.RelativePath.EndsWith("addPet.query-keys.ts", StringComparison.Ordinal));
    }

    // Hybrid: nextjs (server) + axios (client) + TanStack must share ONE {op}.query-keys.ts.
    [Test]
    public async Task Hybrid_NextjsAndAxios_ShareOneCacheKeysModule()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("apartments.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["nextjs", "axios"],
            QueryWrapper = "tanstack-react",
        });

        // The single shared module holds both the client query keys and the server cache tags.
        var keys = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getApartmentInvestor.query-keys.ts", StringComparison.Ordinal));
        keys.Content.Should().Contain("export const getGetApartmentInvestorQueryKey");
        keys.Content.Should().Contain("export const getGetApartmentInvestorCacheTags");

        // Client hook wraps axios and imports the query key from the shared module.
        var query = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getApartmentInvestor.query.ts", StringComparison.Ordinal));
        query.Content.Should().Contain("./getApartmentInvestor.axios");
        query.Content.Should().Contain("getGetApartmentInvestorQueryKey");
        query.Content.Should().Contain("from \"./getApartmentInvestor.query-keys\"");

        // Server client imports the cache tags from the SAME module and feeds them to next.tags.
        var nextjs = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getApartmentInvestor.nextjs.ts", StringComparison.Ordinal));
        nextjs.Content.Should().Contain("from \"./getApartmentInvestor.query-keys\"");
        nextjs.Content.Should().Contain("tags: getGetApartmentInvestorCacheTags(params)");
    }

    // Next.js cache tags mirror the client key: hierarchical, id-inlined resource prefixes.
    [Test]
    public async Task CacheTags_AreHierarchicalAndInlineRouteId()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("apartments.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["nextjs"] });

        var keys = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getApartmentInvestor.query-keys.ts", StringComparison.Ordinal));

        keys.Content.Should().Contain("export const getApartmentInvestorCacheTag = \"getApartmentInvestor\"");
        keys.Content.Should().Contain("\"api:apartments\"");
        keys.Content.Should().Contain("`api:apartments:${params.route.id}`");
        keys.Content.Should().Contain("`api:apartments:${params.route.id}:investor`");
    }
}
