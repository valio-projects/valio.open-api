using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Tests.Fixtures;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>Executes generated request serialization against native FormData, Blob, and Headers values.</summary>
[TestFixture]
[Category("NodeIntegration")]
public sealed class TransportRuntimeIntegrationTests
{
    /// <summary>Exercises Next.js bodyless response decoding alongside a JSON receipt with the same status.</summary>
    /// <param name="exactJsonNumbers">Whether to use the lossless JSON decoder.</param>
    /// <returns>The asynchronous generated-code runtime check.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task NextJs_RespectsDeclaredBodylessStatusesAndDecodesJsonAccepted(bool exactJsonNumbers)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(AcceptedResponsesDocument);
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = ["nextjs"],
            ExactJsonNumbers = exactJsonNumbers,
            ValidationLibrary = "zod4",
        });

        var output = await GeneratedTypeScriptRuntime.Run(context, """
            const { submit } = require(path.join(root, 'api', 'submit', 'submit.nextjs.js'));
            const { submitWithReceipt } = require(path.join(root, 'api', 'submitWithReceipt', 'submitWithReceipt.nextjs.js'));
            (async () => {
              for (const status of [202, 204, 205, 304]) {
                const result = await submit(undefined, undefined, async () => new Response(null, { status }));
                assert.equal(result.status, status);
                assert.equal(result.body, undefined);
                assert.equal(result.isSuccessful, status < 300);
              }
              const error = await submit(undefined, undefined,
                async () => Response.json({ message: 'invalid' }, { status: 400 }));
              assert.deepEqual(error.body, { message: 'invalid' });
              assert.equal(error.isSuccessful, false);
              const receipt = await submitWithReceipt(undefined, undefined,
                async () => Response.json({ receipt: 'accepted' }, { status: 202 }));
              assert.equal(receipt.status, 202);
              assert.equal(receipt.isSuccessful, true);
              assert.deepEqual(receipt.body, { receipt: 'accepted' });
              console.log('next response decoding passed');
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """, "zod4", "lossless-json");

        output.Should().Contain("next response decoding passed");
    }

    /// <summary>Describes bodyless follow-up responses and a separate JSON acceptance receipt.</summary>
    private const string AcceptedResponsesDocument = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Accepted responses", "version": "1" },
          "paths": {
            "/submit": {
              "post": {
                "operationId": "submit",
                "responses": {
                  "202": { "description": "Accepted" },
                  "204": { "description": "No content" },
                  "205": { "description": "Reset content" },
                  "304": { "description": "Not modified" },
                  "400": {
                    "description": "Bad request",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "required": ["message"],
                          "properties": { "message": { "type": "string" } }
                        }
                      }
                    }
                  }
                }
              }
            },
            "/submit-with-receipt": {
              "post": {
                "operationId": "submitWithReceipt",
                "responses": {
                  "202": {
                    "description": "Accepted",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "required": ["receipt"],
                          "properties": { "receipt": { "type": "string" } }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    /// <summary>Uses repeated keys for default and explicit explode, and comma delimiters for explode disabled.</summary>
    /// <remarks>Empty, absent, and null arrays emit no query parameter. The Axios case also retains deep-object and escaped string values.</remarks>
    /// <param name="transport">The generated Fetch, Next.js, or Axios URL serializer.</param>
    /// <param name="exactJsonNumbers">The precise Axios case also preserves both int64 values beyond JavaScript's safe integer range.</param>
    /// <returns>The generated client compilation and assertions against the transmitted URL.</returns>
    [TestCase("fetch", false)]
    [TestCase("nextjs", false)]
    [TestCase("axios", false)]
    [TestCase("axios", true)]
    public async Task QueryArrays_UseOpenApiFormDefaultsAndRespectExplodeOverride(string transport, bool exactJsonNumbers)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(QueryArrayDocument);
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = [transport],
            ExactJsonNumbers = exactJsonNumbers,
            ValidationLibrary = "zod4",
            NextJs = new TypeScriptNextJsOptions { GenerateRevalidateHelpers = false },
        });
        var sources = context.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray();
        await GeneratedTypeScriptCompiler.Compile(sources, $"query arrays with {transport}");

        var output = await GeneratedTypeScriptRuntime.Run(context, $$"""
            const transport = {{JsonSerializer.Serialize(transport)}};
            const exact = {{JsonSerializer.Serialize(exactJsonNumbers)}};
            const { list } = require(path.join(root, 'api', 'list', 'list.' + transport + '.js'));
            let requestUrl;
            const axios = require('axios');
            const client = transport === 'axios'
              ? axios.create({ adapter: async config => {
                  requestUrl = axios.getUri(config);
                  return { config, status: 204, data: undefined, headers: {} };
                } })
              : async url => {
                  requestUrl = url;
                  return new Response(null, { status: 204 });
                };
            (async () => {
              const precise = exact ? ['9007199254740993', '9007199254740995']
                .map(value => new (require('lossless-json').LosslessNumber)(value)) : undefined;
              const filter = transport === 'axios' ? { term: 'kept' } : undefined;
              await list({ ids: ['a,b', 'space + π'], explicit: [0, 2], csv: [1, 3], precise, preciseCsv: precise, filter }, undefined, undefined, client);
              assert.equal(requestUrl.split('&filter')[0], '/items?ids=a%2Cb&ids=space+%2B+%CF%80&explicit=0&explicit=2&csv=1%2C3'
                + (exact ? '&precise=9007199254740993&precise=9007199254740995&preciseCsv=9007199254740993%2C9007199254740995' : ''));
              if (filter) assert.equal(new URL(requestUrl, 'https://fixture.invalid').searchParams.get('filter[term]'), 'kept');
              await list({ ids: [], explicit: null }, undefined, undefined, client);
              assert.equal(requestUrl, '/items');
              await list({}, undefined, undefined, client);
              assert.equal(requestUrl, '/items');
              console.log('query array serialization passed');
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """, "lossless-json");

        output.Should().Contain("query array serialization passed");
    }

    /// <summary>Describes omitted, explicit, and overridden OpenAPI form array serialization settings.</summary>
    private const string QueryArrayDocument = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Query arrays", "version": "1" },
          "paths": {
            "/items": {
              "get": {
                "operationId": "list",
                "parameters": [
                  { "name": "ids", "in": "query", "schema": { "type": "array", "items": { "type": "string" } } },
                  { "name": "explicit", "in": "query", "style": "form", "explode": true, "schema": { "type": ["array", "null"], "items": { "type": "integer" } } },
                  { "name": "csv", "in": "query", "style": "form", "explode": false, "schema": { "type": "array", "items": { "type": "integer" } } },
                  { "name": "precise", "in": "query", "schema": { "type": "array", "items": { "type": "integer", "format": "int64" } } },
                  { "name": "preciseCsv", "in": "query", "style": "form", "explode": false, "schema": { "type": "array", "items": { "type": "integer", "format": "int64" } } },
                  { "name": "filter", "in": "query", "style": "deepObject", "explode": true, "schema": { "type": "object", "properties": { "term": { "type": "string" } } } }
                ],
                "responses": { "204": { "description": "No content" } }
              }
            }
          }
        }
        """;

    /// <summary>Verifies actual wire field values and omitted bodies through each generated transport boundary.</summary>
    /// <param name="transport">The generated HTTP client transport.</param>
    /// <returns>The asynchronous generated-code runtime check.</returns>
    [TestCase("fetch")]
    [TestCase("axios")]
    [TestCase("ky")]
    [TestCase("got")]
    [TestCase("ofetch")]
    [TestCase("undici")]
    [TestCase("nextjs")]
    [TestCase("solid")]
    [TestCase("angular-rxjs")]
    [TestCase("angular-promise")]
    public async Task GeneratedRequests_PreserveMultipartHeadersAndNoContentSemantics(string transport)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(Document());
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = [transport],
            ValidationLibrary = transport == "angular-promise" ? "zod4" : null,
        });
        var captionName = context.IrDocument.Models.SelectMany(model => model.Properties)
            .First(property => property.OriginalName == "Title.Text").Name;
        var output = await GeneratedTypeScriptRuntime.Run(context, $$"""
            const transport = {{JsonSerializer.Serialize(transport)}};
            const Module = require('node:module');
            const originalLoad = Module._load;
            let captured;
            const unexpectedJson = async () => { throw new Error('A bodyless response was decoded as JSON'); };
            const response = () => ({ ok: true, status: 204, statusCode: 204, data: undefined, _data: undefined,
                                      json: unexpectedJson, body: { json: unexpectedJson } });
            async function send(url, options) { captured = options; return response(); }
            const axios = { request: async options => { captured = { ...options, body: options.data }; return response(); } };
            const ky = Object.assign(send, { get: send, post: send, put: send, patch: send, delete: send, head: send });
            class HttpFields {
              constructor(values = {}) { this.values = { ...values }; }
              set(name, value) { return new HttpFields({ ...this.values, [name]: value }); }
            }
            Module._load = function(name, parent, isMain) {
              if (name === 'axios') return { __esModule: true, default: axios };
              if (name === 'ky') return { __esModule: true, default: ky };
              if (name === 'got') return { __esModule: true, default: send };
              if (name === 'ofetch') return { $fetch: { raw: send } };
              if (name === 'undici') return { request: send };
              if (name === '@angular/core') return { Injectable: () => value => value };
              if (name === '@angular/common/http') return { HttpParams: HttpFields, HttpHeaders: HttpFields };
              return originalLoad.call(this, name, parent, isMain);
            };
            globalThis.fetch = send;
            function endpoint(name) {
              if (transport === 'angular-promise') {
                const { UploadsPromiseService } = require(path.join(root, 'api', 'uploadsPromiseService.service.js'));
                const { of } = require('rxjs');
                const http = { post: (url, body, options = {}) => {
                  captured = { ...options, body };
                  return of(options.observe === 'response' ? { status: 204, body: null } : null);
                } };
                const service = new UploadsPromiseService(http);
                return service[name].bind(service);
              }
              if (transport === 'angular-rxjs') {
                const { UploadsService } = require(path.join(root, 'api', 'uploadsService.service.js'));
                const http = { post: async (url, body, options = {}) => { captured = { ...options, body }; } };
                const service = new UploadsService(http);
                return service[name].bind(service);
              }
              return require(path.join(root, 'api', name, name + '.' + transport + '.js'))[name];
            }
            function headers() {
              return new Headers(captured.headers instanceof HttpFields ? captured.headers.values : captured.headers);
            }
            (async () => {
              const form = { file: new File(['picture'], 'photo.bin'), metadata: { published: true }, tags: ['one', null, 'two'],
                             bytes: new Uint8Array([0, 255]), [{{JsonSerializer.Serialize(captionName)}}]: 'wire caption' };
              const mergeHeaders = ['fetch', 'axios', 'nextjs'].includes(transport);
              await endpoint('upload')({ form, headers: { X_Request_ID: 'request-42' } },
                mergeHeaders ? { headers: { 'X-Extra': 'extra' } } : undefined);
              assert(captured.body instanceof FormData, transport + ' did not send FormData');
              assert.equal(headers().get('X-Request-ID'), 'request-42');
              if (mergeHeaders) assert.equal(headers().get('X-Extra'), 'extra');
              assert.equal(headers().has('X-Trace-ID'), false);
              assert.equal(headers().has('Content-Type'), false, 'multipart boundary must be generated by the transport');
              assert.equal(await captured.body.get('file').text(), 'picture');
              assert.equal(captured.body.get('Title.Text'), 'wire caption');
              assert.equal(captured.body.get('metadata'), '{"published":true}');
              assert.deepEqual(captured.body.getAll('tags'), ['one', 'two']);
              assert.deepEqual(Array.from(new Uint8Array(await captured.body.get('bytes').arrayBuffer())), [0, 255]);

              await endpoint('uploadOptional')({});
              assert.equal(captured.body, undefined, transport + ' serialized an absent optional body');

              await endpoint('encoded')({ form: { [{{JsonSerializer.Serialize(captionName)}}]: 'space & + Unicode: π', count: 0, active: false, tags: ['one', 'two'] } });
              assert.equal(typeof captured.body, 'string', transport + ' did not send URL-encoded text');
              const encoded = new URLSearchParams(captured.body);
              assert.equal(encoded.get('Title.Text'), 'space & + Unicode: π');
              assert.equal(encoded.get('count'), '0');
              assert.equal(encoded.get('active'), 'false');
              assert.deepEqual(encoded.getAll('tags'), ['one', 'two']);
              assert.equal(headers().get('Content-Type'), 'application/x-www-form-urlencoded');

              if (transport !== 'angular-rxjs') {
                const result = await endpoint('mixed')({ body: 'input' });
                assert.deepEqual(result, { status: 204, body: undefined, isSuccessful: true, isFailed: false });
                if (transport === 'nextjs' || transport === 'undici')
                  assert.equal(headers().get('Content-Type'), 'application/json');
                if (transport === 'angular-promise')
                  await assert.rejects(async () => endpoint('mixed')({ body: 42 }), 'Promise requests must validate before dispatch');
                if (transport === 'nextjs') {
                  await endpoint('mixed')({ body: 'input' }, { headers: new Headers([['content-type', 'application/custom'], ['x-extra', 'extra']]) });
                  assert.equal(headers().get('Content-Type'), 'application/custom');
                  assert.equal(headers().get('X-Extra'), 'extra');
                  await endpoint('mixed')({ body: 'input' }, { headers: [['x-tuple', 'tuple']] });
                  assert.equal(headers().get('X-Tuple'), 'tuple');
                  assert.equal(headers().get('Content-Type'), 'application/json');
                }
              }
              console.log('transport runtime passed');
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """);

        output.Should().Contain("transport runtime passed");
    }

    /// <summary>Returns undefined for a bodyless 204, reports 401 and 500 as HTTP failures, and forwards caller cancellation.</summary>
    /// <param name="transport">The Fetch or Next.js client issuing the bodyless mutation.</param>
    /// <param name="exactJsonNumbers">Whether Fetch uses the lossless response decoder for otherwise typed bodies.</param>
    /// <returns>The generated client's HTTP status and abort assertions.</returns>
    [TestCase("nextjs", false)]
    [TestCase("fetch", false)]
    [TestCase("fetch", true)]
    public async Task VoidMutation_ResolvesForNoContentAndRejectsHttpFailure(string transport, bool exactJsonNumbers)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(Document());
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = [transport],
            ExactJsonNumbers = exactJsonNumbers,
            ValidationLibrary = "zod4",
        });
        var output = await GeneratedTypeScriptRuntime.Run(context, $$"""
            const endpoint = 'uploadOptional.' + {{JsonSerializer.Serialize(transport)}} + '.js';
            const { uploadOptional } = require(path.join(root, 'api', 'uploadOptional', endpoint));
            const controller = new AbortController();
            (async () => {
              const status = code => async (url, options) => {
                assert.equal(options.signal, controller.signal);
                return new Response(null, { status: code });
              };
              assert.equal(await uploadOptional({}, undefined, controller.signal, status(204)), undefined);
              await assert.rejects(uploadOptional({}, undefined, controller.signal, status(401)), /HTTP 401/);
              await assert.rejects(uploadOptional({}, undefined, controller.signal, status(500)), /HTTP 500/);

              controller.abort();
              const abortedFetch = async (url, options) => options.signal.throwIfAborted();
              await assert.rejects(uploadOptional({}, undefined, controller.signal, abortedFetch), { name: 'AbortError' });
              console.log('void response runtime passed');
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """, "lossless-json");

        output.Should().Contain("void response runtime passed");
    }

    /// <summary>Checks that header exposure preserves a typed 202 result and a typed 404 result.</summary>
    /// <param name="transport">The generated transport whose response headers are exposed.</param>
    /// <returns>The asynchronous generated-code runtime check.</returns>
    [TestCase("axios")]
    [TestCase("fetch")]
    [TestCase("nextjs")]
    public async Task TypedResponse_ExposesLocationAndPreservesDocumentedFailures(string transport)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(Document());
        using var options = JsonDocument.Parse($$"""
            {"transports":["{{transport}}"],"includeResponseHeaders":true,"validation":"zod4"}
            """);
        var rawOptions = options.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value);
        var configured = (TypeScriptGenerationOptions)new TypeScriptOptionsDescriptor().ParseOptions(rawOptions, out var errors)!;
        errors.Should().BeEmpty();
        var context = await harness.RunAsync(document, configured);
        var results = context.GeneratedFiles.Single(file => file.RelativePath.EndsWith("mixed.results.ts", StringComparison.Ordinal));
        results.Content.Should().Contain("headers?: Readonly<Record<string, string | undefined>>");
        var output = await GeneratedTypeScriptRuntime.Run(context, $$"""
            const transport = {{JsonSerializer.Serialize(transport)}};
            const { mixed } = require(path.join(root, 'api', 'mixed', 'mixed.' + transport + '.js'));
            const axios = transport === 'axios' ? require('axios') : undefined;
            const headers = { Location: '/jobs/42', 'X-Request-ID': 'request-42' };
            const client = (status, body, headers) => transport !== 'axios'
              ? async () => Response.json(body, { status, headers })
              : axios.create({ adapter: async config => {
                  assert.equal(config.validateStatus(status), true);
                  return { status, data: body, headers, config };
                } });

            (async () => {
              const accepted = await mixed({ body: 'input' }, undefined, undefined, client(202, 'job-42', headers));
              assert.equal(accepted.status, 202);
              assert.equal(accepted.body, 'job-42');
              assert.equal(accepted.isSuccessful, true);
              assert.equal(accepted.headers.location, '/jobs/42');
              assert.equal(accepted.headers['x-request-id'], 'request-42');
              assert.equal(accepted.headers['Location'], undefined);

              const missing = await mixed({ body: 'input' }, undefined, undefined, client(404, { reason: 'missing' }, {}));
              assert.equal(missing.status, 404);
              assert.equal(missing.isFailed, true);
              assert.deepEqual(missing.body, { reason: 'missing' });
              assert.equal(missing.headers.location, undefined);

              await assert.rejects(mixed({ body: 'input' }, undefined, undefined, client(404, { reason: 42 }, {})));
              console.log('typed response headers runtime passed');
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """);

        output.Should().Contain("typed response headers runtime passed");
    }

    /// <summary>Chooses the schema for the received status and media type and preserves headers on typed and bodyless responses.</summary>
    /// <remarks>
    /// A 400 can carry a JSON integer, plain text, or a typed problem. Media matching accepts case and charset variation.
    /// A wrong schema or an undeclared or missing Content-Type fails instead of exposing a typed error body.
    /// </remarks>
    /// <param name="transport">The generated Axios, Fetch, or Next.js client exercised against the same responses.</param>
    /// <param name="exactJsonNumbers">Whether JSON variants use the lossless decoder before schema validation.</param>
    /// <returns>The generated result type compilation and runtime assertions for statuses, media, and response headers.</returns>
    [TestCase("fetch", false)]
    [TestCase("fetch", true)]
    [TestCase("nextjs", false)]
    [TestCase("nextjs", true)]
    [TestCase("axios", false)]
    [TestCase("axios", true)]
    public async Task ResponseMetadata_SelectsTheSchemaByStatusAndMediaType(string transport, bool exactJsonNumbers)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(OpenApiFixture.Read("Transport/response-metadata-3.0.json"));
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = [transport],
            IncludeResponseHeaders = true,
            ExactJsonNumbers = exactJsonNumbers,
            ValidationLibrary = "zod4",
        });
        var results = context.GeneratedFiles.Single(file => file.RelativePath.EndsWith("readReport.results.ts", StringComparison.Ordinal));
        results.Content.Should().Contain("\"location\"?: string").And.Contain("\"x-request-id\"?: string")
            .And.Contain("number | string | ReportProblem");
        await GeneratedTypeScriptCompiler.Compile(context.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray(),
            $"response media types with {transport}");
        var output = await GeneratedTypeScriptRuntime.Run(context, $$"""
            const transport = {{JsonSerializer.Serialize(transport)}};
            const endpoint = 'readReport.' + transport + '.js';
            const { readReport } = require(path.join(root, 'api', 'readReport', endpoint));
            const reply = (status, data, headers) => transport === 'axios'
              ? require('axios').create({ adapter: async config => ({ config, status, data, headers }) })
              : async () => new Response(data, { status, headers });
            (async () => {
              const read = (status, data, headers) => readReport(undefined, undefined, reply(status, data, headers));
              const json = await read(200, '"report"', { 'Content-Type': 'application/json' });
              assert.equal(json.body, 'report');
              const text = await read(200, 'report', { 'Content-Type': 'text/plain' });
              assert.equal(text.body, 'report');

              const accepted = await read(202, 'accepted without JSON quoting', {
                'Content-Type': 'text/plain; charset=utf-8', Location: '/reports/42'
              });
              assert.equal(accepted.status, 202);
              assert.equal(accepted.body, 'accepted without JSON quoting');
              assert.equal(accepted.headers.location, '/reports/42');

              const bodyless = await read(204, null, { 'X-Request-ID': 'report-42' });
              assert.equal(bodyless.body, undefined);
              assert.equal(bodyless.headers['x-request-id'], 'report-42');

              const numberError = await read(400, '42', { 'Content-Type': 'application/json' });
              assert.equal(numberError.isFailed, true);
              assert.equal(numberError.body, 42);
              const textError = await read(400, '42', { 'Content-Type': 'TEXT/PLAIN; charset=utf-8' });
              assert.equal(textError.body, '42');
              const problem = await read(400, '{"issueCode":"conflict"}', { 'Content-Type': 'Application/Problem+JSON; charset=utf-8' });
              assert.deepEqual(problem.body, { issueCode: 'conflict' });

              await assert.rejects(read(400, '42', { 'Content-Type': 'application/problem+json' }));
              await assert.rejects(read(400, '<error/>', { 'Content-Type': 'application/xml' }), /HTTP 400 unexpected Content-Type/);
              await assert.rejects(read(400, new Uint8Array([52, 50]), {}), /HTTP 400 unexpected Content-Type/);
              console.log('response metadata passed');
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """, "zod4", "lossless-json");

        output.Should().Contain("response metadata passed");
    }

    /// <summary>Creates multipart, optional multipart, and mixed typed/no-body response contracts.</summary>
    /// <returns>The OpenAPI request serialization fixture.</returns>
    private static string Document() => OpenApiFixture.Read("Transport/runtime-contracts-3.0.json");
}
