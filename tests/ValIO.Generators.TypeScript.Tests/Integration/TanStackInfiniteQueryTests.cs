using ValIO.Tests.Fixtures;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>Checks paginated reads through generated React Query factories and the actual Axios transport.</summary>
[TestFixture]
public sealed class TanStackInfiniteQueryTests
{
    /// <summary>Checks the mapper's request type for flat filters, grouped filters and a route-only read.</summary>
    /// <returns>The generation and inspection of the three operation-specific modules.</returns>
    [Test]
    public async Task Mapper_ExposesOnlyTheOperationsQueryParameters()
    {
        using var harness = new PipelineTestHarness();
        var document = await OpenApiFixture.LoadAsync("TypeScript/apartments.json");
        var generated = await harness.RunAsync(document, new TypeScriptGenerationOptions { Transports = ["axios"], QueryWrapper = "tanstack-react" });
        var modules = generated.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".query.ts", StringComparison.Ordinal))
            .ToDictionary(file => Path.GetFileName(file.RelativePath), file => file.Content);
        modules["listApartments.query.ts"].Should().Contain("getPageQuery?: (pageParam: number) => Partial<ListApartmentsParams>");
        modules["getApartmentInvestor.query.ts"].Should().Contain("getPageQuery?: (pageParam: number) => Partial<NonNullable<GetApartmentInvestorParams[\"query\"]>>");
        modules["getApartment.query.ts"].Should().NotContain("getPageQuery");
    }

    /// <summary>Reads multiple pages with a real QueryClient, maintaining request options, route, filters and cancellation.</summary>
    /// <returns>The execution of the generated TypeScript factories and captured Axios requests.</returns>
    [Test, Category("NodeIntegration")]
    public async Task QueryClient_RequestsDistinctPagesWithoutMutatingOriginalFilters()
    {
        using var harness = new PipelineTestHarness();
        var document = await OpenApiFixture.LoadAsync("TypeScript/apartments.json");
        var generated = await harness.RunAsync(document, new TypeScriptGenerationOptions { Transports = ["axios"], QueryWrapper = "tanstack-react" });
        var files = generated.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToList();
        files.Add(new GeneratedFile("infinite-query-consumer.ts", """
            import { getListApartmentsInfiniteQueryOptions as flat } from './api/listApartments/listApartments.query';
            import { getGetApartmentInvestorInfiniteQueryOptions as grouped } from './api/getApartmentInvestor/getApartmentInvestor.query';
            flat({ city: 'Oslo' }, { getPageQuery: page => ({ page }), query: {
              initialPageParam: 2, getNextPageParam: (_, pages, previous) => pages.length < 2 ? previous + 1 : undefined,
            } });
            grouped({ route: { id: 'fixed' } }, { getPageQuery: page => ({ tax: String(page) }) });
            // @ts-expect-error A page mapping must return fields from the query group.
            grouped({ route: { id: 'fixed' } }, { getPageQuery: () => ({ id: 'other' }) });
            """));
        await GeneratedTypeScriptCompiler.Compile(files, "Infinite query consumer maps pages to declared query fields");
        var output = await GeneratedTypeScriptRuntime.Run(generated, """
            const { QueryClient } = require('@tanstack/react-query');
            const { getListApartmentsInfiniteQueryOptions: flat } = require(path.join(root, 'api/listApartments/listApartments.query.js'));
            const { getGetApartmentInvestorInfiniteQueryOptions: grouped } = require(path.join(root, 'api/getApartmentInvestor/getApartmentInvestor.query.js'));
            const { getGetApartmentInfiniteQueryOptions: single } = require(path.join(root, 'api/getApartment/getApartment.query.js'));
            const requests = [];
            const request = { adapter: async config => {
              requests.push(config);
              return { status: 200, statusText: 'OK', headers: {}, data: [], config };
            } };
            (async () => {
              const client = new QueryClient();
              const filters = Object.freeze({ city: 'Oslo', page: 99 });
              const options = flat(filters, {
                request,
                query: { queryKey: ['manual'], initialPageParam: 2,
                         getNextPageParam: (_, pages, previous) => pages.length < 2 ? previous + 1 : undefined },
                getPageQuery: page => ({ page }),
              });
              const result = await client.fetchInfiniteQuery({ ...options, pages: 3 });
              assert.deepEqual(result.pageParams, [2, 3]);
              assert.deepEqual(requests.map(config => config.params), [{ city: 'Oslo', page: 2 }, { city: 'Oslo', page: 3 }]);
              assert(requests.every(config => config.signal instanceof AbortSignal));
              assert.deepEqual(options.queryKey, ['manual']);
              assert.equal(filters.page, 99);

              const controller = new AbortController();
              const params = Object.freeze({ route: Object.freeze({ id: 'building-7' }), query: Object.freeze({ entity: 'fixed' }) });
              await grouped(params, { request, getPageQuery: page => ({ tax: String(page) }) }).queryFn({ pageParam: 4, signal: controller.signal });
              const nested = requests.at(-1);
              assert.equal(nested.url, '/api/apartments/building-7/investor');
              assert.deepEqual(nested.params, { entity: 'fixed', tax: '4' });
              assert.equal(nested.signal, controller.signal);
              const fallback = single({ id: 'building-7' }, { request });
              assert.equal(fallback.initialPageParam, 0);
              assert.equal(fallback.getNextPageParam(), null);

              let reportDispatch;
              const started = new Promise(resolve => { reportDispatch = resolve; });
              const cancelled = client.fetchInfiniteQuery(flat(filters, {
                query: { queryKey: ['cancel'] },
                request: { adapter: config => new Promise((_, reject) => {
                  config.signal.addEventListener('abort', () => reject(new Error('cancelled')), { once: true });
                  reportDispatch(config.signal);
                }) },
              })).then(() => false, () => true);
              const signal = await started;
              await client.cancelQueries({ queryKey: ['cancel'] });
              assert.equal(signal.aborted, true);
              assert.equal(await cancelled, true);
              client.clear();
              console.log('infinite query paging passed');
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """);
        output.Should().Contain("infinite query paging passed");
    }
}
