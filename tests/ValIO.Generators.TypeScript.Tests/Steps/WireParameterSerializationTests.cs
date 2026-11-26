using ValIO.Tests.Fixtures;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Verifies that TypeScript HTTP transports retain OpenAPI parameter names on the wire after sanitizing their
/// TypeScript property identifiers.
/// </summary>
[TestFixture]
public sealed class WireParameterSerializationTests
{
    /// <summary>
    /// Ensures dotted query names, hyphenated header names, and sanitized path placeholders are serialized
    /// correctly by each fetch-family transport and Axios.
    /// </summary>
    /// <param name="transport">The TypeScript transport under test.</param>
    /// <param name="validationLibrary">The Zod runtime generation variant under test.</param>
    /// <returns>A task that completes after the transport output has been verified.</returns>
    [TestCase("axios", "zod3")]
    [TestCase("fetch", "zod3")]
    [TestCase("nextjs", "zod3")]
    [TestCase("axios", "zod4")]
    [TestCase("fetch", "zod4")]
    [TestCase("nextjs", "zod4")]
    public async Task TransportUsesOriginalNamesOnWireAndSanitizedNamesForAccess(string transport, string validationLibrary)
    {
        using var harness = new PipelineTestHarness();
        var document = await OpenApiFixture.LoadAsync("Transport/wire-parameter-names.json");
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = [transport],
            ValidationLibrary = validationLibrary,
        });

        var endpoint = context.GeneratedFiles.Single(file =>
            file.RelativePath.EndsWith($"getWidget.{transport}.ts", StringComparison.Ordinal));
        var parameterSchema = context.GeneratedFiles.Single(file =>
            file.RelativePath.EndsWith("getWidget.params.schema.ts", StringComparison.Ordinal));

        endpoint.Content.Should().Contain("parsedQuery.Paging_Offset");
        endpoint.Content.Should().Contain("X_Request_ID");
        endpoint.Content.Should().Contain("Filter_By");
        endpoint.Content.Should().Contain("X_Trace_ID");
        endpoint.Content.Should().Contain("parsedRoute.item_id");
        // The AST supplies raw names; the renderer applies exactly one layer of TypeScript string quoting.
        // A pre-quoted key would serialize the literal quotes, e.g. '"Paging.Offset"', instead.
        endpoint.Content.Should().NotContain("'\"Paging.Offset\"':");
        endpoint.Content.Should().NotContain("'\"Filter.By\"':");
        endpoint.Content.Should().NotContain("'\"X-Request-ID\"':");
        endpoint.Content.Should().NotContain("'\"X-Trace-ID\"':");
        endpoint.Content.Should().NotContain("\"Paging_Offset\":");
        endpoint.Content.Should().NotContain("\"X_Request_ID\":");
        endpoint.Content.Should().NotContain("{item-id}");
        endpoint.Content.Should().NotContain("query.set(\"Paging_Offset\"");
        endpoint.Content.Should().NotContain("\"Filter.By\": undefined");
        endpoint.Content.Should().NotContain("\"X-Trace-ID\": undefined");

        if (transport == "axios")
        {
            endpoint.Content.Should().NotContain("_: ");
            endpoint.Content.Should().Contain("'Paging.Offset': parsedQuery.Paging_Offset");
            endpoint.Content.Should().Contain("'Filter.By': parsedQuery.Filter_By");
            endpoint.Content.Should().Contain("'X-Request-ID': params.headers.X_Request_ID");
            endpoint.Content.Should().Contain("'X-Trace-ID': params.headers.X_Trace_ID");
            endpoint.Content.Should().Contain("UserId: parsedQuery.UserId");
            endpoint.Content.Should().NotContain("\"UserId\": parsedQuery.UserId");
        }
        else
        {
            endpoint.Content.Should().Contain("!== undefined");
            endpoint.Content.Should().Contain("\"Paging.Offset\",");
            endpoint.Content.Should().Contain("\"Filter.By\",");
            endpoint.Content.Should().Contain("\"UserId\",");
            endpoint.Content.Should().Contain("String(parsedQuery.UserId)");
            endpoint.Content.Should().MatchRegex("requestHeaders\\.set\\(\\s+\"X-Request-ID\",\\s+String\\(params\\.headers\\.X_Request_ID\\)\\s+\\)");
            endpoint.Content.Should().MatchRegex("requestHeaders\\.set\\(\\s+\"X-Trace-ID\",\\s+String\\(params\\.headers\\.X_Trace_ID\\)\\s+\\)");
            endpoint.Content.Should().NotContain("as Record<string, string>) ?? {}");
        }

        if (validationLibrary == "zod4")
            parameterSchema.Content.Should().Contain("from \"zod/v4\"");
    }

}
