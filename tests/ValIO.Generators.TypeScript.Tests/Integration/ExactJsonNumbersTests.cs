using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>Exercises the generated numeric types and the transport boundaries of exact JSON.</summary>
[TestFixture]
[Category("NodeIntegration")]
public sealed class ExactJsonNumbersTests
{
    /// <summary>Compiles and runs provider-style scalar references and numeric alternatives.</summary>
    /// <returns>The generated type, request text, and lossless Next.js response assertions.</returns>
    [Test]
    public async Task GeneratedNextJsPreservesNamedScalarsAndNumericAlternatives()
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(OpenApiFixture.Read("CoreIR/native-scalar-union-3.0.json"));
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = ["nextjs"],
            ValidationLibrary = "zod4",
            ExactJsonNumbers = true,
        });

        context.GeneratedFiles.Single(file => file.RelativePath == "models/actionCode/actionCode.ts").Content
            .Should().Contain("type ActionCode = number");
        var sources = context.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray();
        await GeneratedTypeScriptCompiler.Compile(sources, "native scalar aliases and numeric alternatives");

        var output = await GeneratedTypeScriptRuntime.Run(context, """
            const { LosslessNumber } = require('lossless-json');
            const { ActionCodeSchema } = require(path.join(root, 'models', 'actionCode', 'actionCode.schema.js'));
            const { readPage } = require(path.join(root, 'api', 'readPage', 'readPage.nextjs.js'));
            assert.equal(ActionCodeSchema.parse(1), 1);
            for (const invalid of [{}, '1', -1, 1.5]) {
                assert.equal(ActionCodeSchema.safeParse(invalid).success, false);
            }
            const width = '9007199254740993';
            let body = '{"action":1,"total":' + width + ',"latest":' + width + '}';
            const fetcher = async url => {
                assert.equal(url, '/pages?cursor=' + width);
                return new Response(body, { headers: { 'Content-Type': 'application/json' } });
            };
            (async () => {
                const number = await readPage({ cursor: new LosslessNumber(width) }, undefined, undefined, fetcher);
                assert(number.body.total instanceof LosslessNumber);
                assert.equal(number.body.total.toString(), width);
                assert.equal(number.body.latest.toString(), width);
                assert.equal(number.body.action, 1);

                body = '{"action":1,"total":"' + width + '","latest":' + width + '}';
                const text = await readPage({ cursor: width }, undefined, undefined, fetcher);
                assert.equal(text.body.total, width);
                body = '{"action":{},"total":' + width + ',"latest":' + width + '}';
                await assert.rejects(() => readPage({ cursor: width }, undefined, undefined, fetcher));
                console.log('native scalar contracts passed');
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """, "lossless-json");
        output.Should().Contain("native scalar contracts passed");
    }

    /// <summary>Exercises lossless numeric serialization and typed response validation through the generated transport.</summary>
    /// <remarks>
    /// The HTTP request keeps exact JSON decimals, int64 route, query and header strings, and an int64 query default.
    /// The response retains the full numeric(28,10) amount and numeric enum zero. Quoted numbers, fractional integers, invalid enum members,
    /// overflowing revisions, and out-of-range amounts fail validation. A declared 204 remains bodyless.
    /// </remarks>
    /// <param name="transport">The Axios, Next.js, or Fetch client used to run the same wire assertions.</param>
    /// <returns>The generated TypeScript compilation and runtime assertions.</returns>
    [TestCase("axios")]
    [TestCase("nextjs")]
    [TestCase("fetch")]
    public async Task GeneratedClient_PreservesExactNumbersAcrossTheWire(string transport)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(Document);
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = [transport],
            ValidationLibrary = "zod4",
            ExactJsonNumbers = true,
            IncludeResponseHeaders = true,
            EnumFormat = "Enum",
        });

        context.GeneratedFiles.Single(file => file.RelativePath == "models/change/change.ts").Content
            .Should().Contain("import(\"lossless-json\").LosslessNumber");

        var sources = context.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray();
        await GeneratedTypeScriptCompiler.Compile(sources, $"exact numbers with {transport}");

        var output = await GeneratedTypeScriptRuntime.Run(context, $$"""
            const transport = {{JsonSerializer.Serialize(transport)}};
            const { LosslessNumber } = require('lossless-json');
            const { save } = require(path.join(root, 'api', 'save', 'save.' + transport + '.js'));
            const decimal = '0.100000000000000001';
            const ledgerAmount = '999999999999999999.9999999999';
            const revision = '9007199254740993';
            let expectedAfter = revision;
            const request = {
              route: { revision: new LosslessNumber(revision) },
              query: { after: new LosslessNumber(revision) },
              headers: { X_Revision: new LosslessNumber(revision) },
              body: {
                revision: new LosslessNumber(revision),
                amount: new LosslessNumber(decimal),
                ledgerAmount: new LosslessNumber(ledgerAmount),
                rates: [1.25], label: revision, count: 0, otherAmount: null, state: 0
              }
            };
            const document = '{"revision":' + revision + ',"amount":' + decimal
              + ',"ledgerAmount":' + ledgerAmount
              + ',"rates":[1.25],"label":"' + revision + '","count":0,"otherAmount":null,"state":0}';
            let status = 200;
            let responseBody = document;

            function checkRequest(url, body, headers, signal) {
              assert.equal(url, '/changes/' + revision + '?after=' + expectedAfter);
              assert(body.includes('"revision":' + revision + ','));
              assert(body.includes('"amount":' + decimal + ','));
              assert(body.includes('"ledgerAmount":' + ledgerAmount + ','));
              assert(body.includes('"label":"' + revision + '"'));
              assert.equal(JSON.parse(body).count, 0);
              assert.equal(JSON.parse(body).state, 0);
              assert.equal(headers.get('content-type'), 'application/json');
              assert.equal(headers.get('x-revision'), revision);
              assert(signal instanceof AbortSignal);
            }

            const axios = require('axios');
            const client = transport === 'axios'
              ? axios.create({ adapter: async config => {
                  checkRequest(axios.getUri(config), config.data, config.headers, config.signal);
                  return { config, status, statusText: 'fixture', data: responseBody,
                    headers: { 'Content-Type': 'application/json', ETag: '"version"' } };
                } })
              : async (url, options) => {
                  checkRequest(url, options.body, new Headers(options.headers), options.signal);
                  return new Response(responseBody || null, {
                    status, headers: { 'Content-Type': 'application/json', ETag: '"version"' }
                  });
                };

            (async () => {
              const signal = new AbortController().signal;
              const result = await save(request, undefined, signal, client);
              assert.equal(result.status, 200);
              assert.equal(result.body.revision.toString(), revision);
              assert.equal(result.body.amount.toString(), decimal);
              assert.equal(result.body.ledgerAmount.toString(), ledgerAmount);
              assert.equal(result.body.label, revision);
              assert.deepEqual(result.body.rates, [1.25]);
              assert.equal(result.body.count, 0);
              assert.equal(result.body.state, 0);
              assert.equal(result.body.otherAmount, null);
              assert.equal(result.body.omitted, undefined);
              assert.equal(result.headers.etag, '"version"');

              expectedAfter = '9007199254740995';
              await save({ ...request, query: undefined }, undefined, signal, client);
              expectedAfter = revision;

              responseBody = document.replace('"amount":' + decimal, '"amount":"' + decimal + '"');
              await assert.rejects(() => save(request, undefined, signal, client));
              responseBody = document.replace('"count":0', '"count":1.000000000000000001');
              await assert.rejects(() => save(request, undefined, signal, client));
              responseBody = document.replace('"state":0', '"state":"0"');
              await assert.rejects(() => save(request, undefined, signal, client));
              responseBody = document.replace('"state":0', '"state":2');
              await assert.rejects(() => save(request, undefined, signal, client));
              responseBody = document.replace('"revision":' + revision, '"revision":9223372036854775808');
              await assert.rejects(() => save(request, undefined, signal, client));
              responseBody = document.replace('"amount":' + decimal, '"amount":0.100000000000000000');
              await assert.rejects(() => save(request, undefined, signal, client));
              responseBody = document.replace('"ledgerAmount":' + ledgerAmount, '"ledgerAmount":1000000000000000000.0');
              await assert.rejects(() => save(request, undefined, signal, client));

              status = 204;
              responseBody = '';
              const noContent = await save(request, undefined, signal, client);
              assert.equal(noContent.status, 204);
              assert.equal(noContent.body, undefined);
              console.log('exact JSON numbers preserved');
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """, "lossless-json", "axios");
        output.Should().Contain("exact JSON numbers preserved");
    }

    /// <summary>Describes a numeric OpenAPI contract with ordinary and exact fields sharing a JSON model.</summary>
    private const string Document = """
        {
          "openapi":"3.1.0",
          "info":{"title":"Exact JSON","version":"1.0"},
          "paths":{
            "/changes/{revision}":{
              "post":{
                "operationId":"save",
                "parameters":[
                  {"name":"revision","in":"path","required":true,"schema":{"type":"integer","format":"int64"}},
                  {"name":"after","in":"query","schema":{"type":"integer","format":"int64","default":9007199254740995}},
                  {"name":"X-Revision","in":"header","required":true,"schema":{"type":"integer","format":"int64"}}
                ],
                "requestBody":{"required":true,"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Change"}}}},
                "responses":{
                  "200":{"description":"Saved","content":{"application/json":{"schema":{"$ref":"#/components/schemas/Change"}}}},
                  "204":{"description":"No content"}
                }
              }
            }
          },
          "components":{"schemas":{"ChangeState":{"type":"integer","format":"int32","enum":[0,1]},"Change":{
            "type":"object",
            "required":["revision","amount","ledgerAmount","rates","label","count","otherAmount","state"],
            "properties":{
              "revision":{"type":"integer","format":"int64"},
              "amount":{"type":"number","format":"decimal","minimum":0.100000000000000001},
              "ledgerAmount":{"type":"number","format":"decimal","maximum":999999999999999999.9999999999},
              "rates":{"type":"array","items":{"type":"number","format":"double"}},
              "label":{"type":"string"},
              "count":{"type":"integer","format":"int32"},
              "state":{"$ref":"#/components/schemas/ChangeState"},
              "otherAmount":{"type":["number","null"],"format":"decimal"},
              "omitted":{"type":"number","format":"decimal"}
            }
          }}}
        }
        """;
}
