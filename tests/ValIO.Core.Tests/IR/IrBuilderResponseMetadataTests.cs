using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Builders;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Services;
using ValIO.Core.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Core.Tests.IR;

/// <summary>Protects status-specific response metadata and the legacy payload type.</summary>
[TestFixture]
public sealed class IrBuilderResponseMetadataTests
{
    /// <summary>Checks that bodyless and typed statuses retain their declarations without changing the selected payload.</summary>
    /// <returns>The asynchronous OpenAPI parsing and IR assertions.</returns>
    [Test]
    public async Task ResponsesPreserveMediaTypesAndHeaderWireNames()
    {
        var document = await IrTestHarness.LoadAsync(OpenApiFixture.Read("Transport/response-metadata-3.0.json"));
        var builder = new IrBuilder(new SchemaResolver(document), new NamingConverter());
        var responses = builder.BuildFromOpenApi(document).Clients.Single().Operations.Single().Responses;

        var accepted = responses.Single(response => response.StatusCode == "202");
        accepted.ContentTypes.Should().Equal("text/plain");
        var location = accepted.Headers.Should().ContainSingle().Which;
        location.Name.Should().Be("Location");
        location.IsRequired.Should().BeTrue();
        location.Description.Should().Be("The URL for reading the completed report.");

        var bodyless = responses.Single(response => response.StatusCode == "204");
        bodyless.ContentTypes.Should().BeEmpty();
        bodyless.Type.Should().BeNull();
        var requestId = bodyless.Headers.Should().ContainSingle().Which;
        requestId.Name.Should().Be("X-Request-ID");
        requestId.IsRequired.Should().BeFalse();
        requestId.Description.Should().Be("The request correlation identifier.");

        var invalid = responses.Single(response => response.StatusCode == "400");
        invalid.ContentTypes.Should().Equal("application/json", "text/plain");
        invalid.Type.Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.Integer);
    }
}
