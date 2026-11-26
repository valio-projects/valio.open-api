using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>Exercises the generated Next.js download against an HTTP server with a delayed body.</summary>
[TestFixture]
[Category("NodeIntegration")]
public sealed class NextJsBinaryStreamingTests
{
    /// <summary>Compiles and runs binary download, range, failure, and cancellation contracts.</summary>
    /// <param name="exactJsonNumbers">Whether the JSON error decoder preserves exact numeric tokens.</param>
    /// <returns>The isolated compilation and HTTP assertions.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task DownloadsReturnTheUnconsumedNativeResponseStream(bool exactJsonNumbers)
    {
        using var harness = new PipelineTestHarness();
        var document = await OpenApiFixture.LoadAsync("CoreIR/csharp-binary-response-3.0.json");
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = ["nextjs"],
            ValidationLibrary = "zod4",
            ExactJsonNumbers = exactJsonNumbers,
        });

        var result = context.GeneratedFiles.Single(file => file.RelativePath == "api/readDocument/readDocument.results.ts");
        result.Content.Should().Contain("body: ReadableStream<Uint8Array>").And.Contain("headers?:");
        context.GeneratedFiles.Should().NotContain(file => file.RelativePath == "api/readDocument/readDocument.results.schemas.ts");

        var sources = context.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray();
        await GeneratedTypeScriptCompiler.Compile(sources, "Next.js binary streaming");
        var output = await GeneratedTypeScriptRuntime.Run(context, Runtime, "lossless-json");
        output.Should().Contain("Next.js binary HTTP streaming passed");
    }

    /// <summary>Starts a loopback server whose first successful body depends on the client returning after headers.</summary>
    private const string Runtime = """
        const {createServer} = require('node:http');
        const {once} = require('node:events');
        const {ReadDocument} = require(path.join(root, 'api', 'readDocument', 'readDocument.nextjs.js'));
        const fullBody = Buffer.alloc(64 * 1024, 0xab);
        const release = Promise.withResolvers();
        const observedClose = new Map();
        const requestReceived = Promise.withResolvers();
        const server = createServer(async (request, response) => {
          const scenario = request.headers['x-fixture'];
          try {
            assert.equal(request.url, '/documents/content');
            if (scenario === 'full') {
              response.writeHead(200, {'Content-Type': 'application/pdf', 'Transfer-Encoding': 'chunked'});
              response.flushHeaders();
              await release.promise;
              response.write(fullBody.subarray(0, 1024));
              response.end(fullBody.subarray(1024));
            } else if (scenario === 'range') {
              assert.equal(request.headers.range, 'bytes=5-7');
              response.writeHead(206, {
                'Content-Type': 'application/octet-stream', 'Content-Range': 'bytes 5-7/40',
                'Content-Disposition': 'attachment; filename="original.bin"', 'Content-Length': 3,
              });
              response.end('xyz');
            } else if (scenario === 'missing') {
              response.writeHead(404, {'Content-Type': 'application/problem+json', 'Cache-Control': 'private, no-store'});
              response.end(readFixture('Payloads/Http/binary-not-found.json'));
            } else {
              assert(observedClose.has(scenario));
              response.once('close', observedClose.get(scenario).resolve);
              if (scenario === 'before-headers') {
                requestReceived.resolve();
              } else {
                response.writeHead(200, {'Content-Type': 'image/png', 'Transfer-Encoding': 'chunked'});
                response.flushHeaders();
              }
            }
          } catch (error) {
            response.destroy(error);
          }
        });
        server.listen(0, '127.0.0.1');

        (async () => {
          try {
            await once(server, 'listening');
            const origin = 'http://127.0.0.1:' + server.address().port;
            let native;
            const client = async (url, init) => {
              assert.equal(init.cache, 'no-store');
              assert.equal(init.next, undefined);
              native = await fetch(origin + url, init);
              return native;
            };
            function send(scenario, signal = AbortSignal.timeout(8000), headers = {}) {
              return ReadDocument({}, {
                cache: 'force-cache',
                next: {revalidate: 86400, tags: ['never-cache-private-download']},
                headers: {'X-Fixture': scenario, ...headers},
              }, signal, client);
            }
            const full = await send('full');
            assert.equal(full.status, 200);
            assert.equal(full.isSuccessful, true);
            assert.equal(full.headers['content-type'], 'application/pdf');
            assert.equal(full.headers['transfer-encoding'], 'chunked');
            assert.equal(native.bodyUsed, false);
            assert.strictEqual(full.body, native.body);
            release.resolve();
            const chunks = [];
            for await (const chunk of full.body) chunks.push(chunk);
            assert.deepEqual(Buffer.concat(chunks), fullBody);

            const range = await send('range', undefined, {Range: 'bytes=5-7'});
            assert.equal(range.status, 206);
            assert.equal(range.headers['content-range'], 'bytes 5-7/40');
            assert.equal(range.headers['content-length'], '3');
            assert.equal(range.headers['content-disposition'], 'attachment; filename="original.bin"');
            const partial = [];
            for await (const chunk of range.body) partial.push(chunk);
            assert.equal(Buffer.concat(partial).toString(), 'xyz');

            const missing = await send('missing');
            assert.equal(missing.status, 404);
            assert.equal(missing.isFailed, true);
            assert.equal(missing.body.title, 'Document missing');
            assert.equal(missing.headers['cache-control'], 'private, no-store');

            for (const scenario of ['cancel-body', 'abort-body', 'before-headers']) {
              const closed = Promise.withResolvers();
              observedClose.set(scenario, closed);
              const controller = new AbortController();
              const signal = AbortSignal.any([controller.signal, AbortSignal.timeout(8000)]);
              const request = send(scenario, signal);
              if (scenario === 'before-headers') {
                await requestReceived.promise;
                controller.abort();
                await assert.rejects(request, {name: 'AbortError'});
              } else {
                const response = await request;
                if (scenario === 'cancel-body') {
                  await response.body.cancel();
                } else {
                  const reader = response.body.getReader();
                  const pending = reader.read();
                  controller.abort();
                  await assert.rejects(pending, {name: 'AbortError'});
                  reader.releaseLock();
                }
              }
              await Promise.race([
                closed.promise,
                new Promise((_, reject) => setTimeout(() => reject(new Error('Response connection stayed open.')), 3000).unref()),
              ]);
            }
            console.log('Next.js binary HTTP streaming passed');
          } finally {
            release.resolve();
            server.closeAllConnections();
            server.close();
          }
        })().catch(error => { console.error(error); process.exitCode = 1; });
        """;
}
