using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>Exercises generated client behavior and generated TypeScript types.</summary>
[TestFixture]
public sealed class ClientExecutionContractTests
{
    /// <summary>Compiles nullable array elements and inherited reference intersections in strict mode.</summary>
    /// <returns>The asynchronous strict TypeScript compilation.</returns>
    [Test]
    [Category("NodeIntegration")]
    public async Task NestedNullableArraysAndInheritedReferencesCompile()
    {
        const string json = """
        {"openapi":"3.0.3","info":{"title":"types","version":"1"},"paths":{},"components":{"schemas":{
          "ValueModel":{"type":"object","properties":{"name":{"type":"string"}}},
          "Base":{"type":"object","properties":{"value":{"$ref":"#/components/schemas/ValueModel"}}},
          "Middle":{"allOf":[{"$ref":"#/components/schemas/Base"}]},
          "Child":{"allOf":[{"$ref":"#/components/schemas/Middle"},{"type":"object","properties":{"value":{"type":"string"}},"required":["value"]}]},
          "Nested":{"type":"object","required":["items","scalars"],"properties":{
            "items":{"type":"array","items":{"type":"array","nullable":true,"items":{"type":"string"}}},
            "scalars":{"type":"array","items":{"type":"string","nullable":true}}
          }}
        }}}
        """;
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(json);
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions { Transports = ["fetch"] });
        context.GeneratedFiles.Add(new GeneratedFile("check.ts", """
            import type { Nested } from './models/nested/nested';
            const ok: Nested = { items: [['x'], null], scalars: ['x', null] };
            // @ts-expect-error nullable item is not a nullable outer array
            const bad: Nested = { items: null, scalars: [] };
            void ok; void bad;
            """));
        await GeneratedTypeScriptCompiler.Compile(context.GeneratedFiles
            .Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray(), "nested nullable arrays and inherited references");
    }

    /// <summary>Executes mixed and bodyless results through real Axios and native fetch response objects.</summary>
    /// <returns>The asynchronous generated response decoding check.</returns>
    [Test]
    [Category("NodeIntegration")]
    public async Task ClientsHandleBodylessSuccessesWithoutDecodingJson()
    {
        const string json = """
        {"openapi":"3.0.3","info":{"title":"responses","version":"1"},"paths":{
          "/mixed":{"get":{"operationId":"mixed","responses":{
            "200":{"description":"value","content":{"application/json":{"schema":{"type":"string"}}}},
            "204":{"description":"empty"}}}},
          "/empty":{"get":{"operationId":"empty","responses":{"204":{"description":"empty"}}}}
        }}
        """;
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(json);
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions { Transports = ["axios", "fetch"] });
        await GeneratedTypeScriptRuntime.Run(context, """
            const axios = require('axios');
            let status = 204, jsonCalls = 0;
            const client = axios.create({ adapter: async config => ({status,data:status === 204 ? undefined : 'world',statusText:'',headers:{},config}) });
            globalThis.fetch = async () => {
              const response = new Response(status === 204 ? null : JSON.stringify('world'), {status});
              const json = response.json.bind(response);
              response.json = () => { jsonCalls++; return json(); };
              return response;
            };
            function endpoint(name, transport) { return require(path.join(root, 'api', name, name + '.' + transport + '.js'))[name]; }
            (async () => {
              for (const transport of ['axios','fetch']) {
                status = 204;
                const mixed = endpoint('mixed', transport), empty = endpoint('empty', transport);
                const result = await mixed(undefined, undefined, transport === 'axios' ? client : undefined);
                assert.equal(result.status, 204); assert.equal(result.body, undefined); assert.equal(result.isSuccessful, true);
                assert.equal(await empty(undefined, undefined, transport === 'axios' ? client : undefined), undefined);
                assert.equal(jsonCalls, 0);
                status = 200;
                assert.equal((await mixed(undefined, undefined, transport === 'axios' ? client : undefined)).body, 'world');
              }
              assert.equal(jsonCalls, 1);
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """);
    }

    /// <summary>Preserves the original OpenAPI query name across generated transport clients.</summary>
    /// <param name="transport">The selected TypeScript transport.</param>
    /// <returns>The asynchronous transport generation check.</returns>
    [TestCase("axios")]
    [TestCase("fetch")]
    [TestCase("nextjs")]
    [TestCase("solid")]
    [TestCase("ky")]
    [TestCase("got")]
    [TestCase("ofetch")]
    [TestCase("undici")]
    [TestCase("angular-rxjs")]
    [TestCase("angular-promise")]
    public async Task TransportClientsPreserveOriginalQueryNames(string transport)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(QueryContract);
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions { Transports = [transport] });
        var suffix = transport.StartsWith("angular-", StringComparison.Ordinal) ? ".service.ts" : $".{transport}.ts";
        var client = context.GeneratedFiles.Single(file => file.RelativePath.EndsWith(suffix, StringComparison.Ordinal));
        client.Content.Should().MatchRegex(@"[""']Paging\.Page[""']");
        client.Content.Should().MatchRegex(@"\?*\.Paging_Page\b");
    }

    /// <summary>Executes original query names through real Axios and URLSearchParams boundaries.</summary>
    /// <returns>The asynchronous query serialization check.</returns>
    [Test]
    [Category("NodeIntegration")]
    public async Task QueryParametersUseOriginalWireNamesAtRuntime()
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(QueryContract);
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions { Transports = ["axios", "fetch"] });
        await GeneratedTypeScriptRuntime.Run(context, """
            const axios = require('axios');
            let config, url;
            const client = axios.create({adapter: async value => { config = value; return {status:204,data:undefined,statusText:'',headers:{},config:value}; }});
            globalThis.fetch = async value => { url = value; return new Response(null, {status:204}); };
            const axiosCall = require(path.join(root, 'api/listItems/listItems.axios.js')).listItems;
            const fetchCall = require(path.join(root, 'api/listItems/listItems.fetch.js')).listItems;
            (async () => {
              await axiosCall({Paging_Page:3,Status:'active'},undefined,undefined,client);
              assert.equal(config.params['Paging.Page'],3); assert.equal(config.params.Status,'active');
              assert.equal(Object.hasOwn(config.params,'Paging_Page'),false);
              await fetchCall({Paging_Page:3,Status:'active'});
              const query = new URL(url,'http://example.test').searchParams;
              assert.equal(query.get('Paging.Page'),'3'); assert.equal(query.get('Status'),'active'); assert.equal(query.has('Paging_Page'),false);
              await fetchCall({Status:'active'});
              assert.equal(new URL(url,'http://example.test').searchParams.has('Paging.Page'),false);
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """);
    }

    /// <summary>Defines sanitized identifiers and original query names for generation and runtime checks.</summary>
    private const string QueryContract = """
        {"openapi":"3.0.3","info":{"title":"wire","version":"1"},"paths":{"/items":{"get":{
          "operationId":"listItems","parameters":[
            {"name":"Paging.Page","in":"query","schema":{"type":"integer"}},
            {"name":"Status","in":"query","schema":{"type":"string"}}],
          "responses":{"204":{"description":"empty"}}}}}}
        """;
}
