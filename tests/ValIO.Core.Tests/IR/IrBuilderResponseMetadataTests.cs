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
    /// <summary>Retains Location for 202, optional request ID for 204, and distinct schemas for three media types under 400.</summary>
    /// <returns>The assertions for header casing, requiredness, and the selected legacy payload type.</returns>
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
        invalid.ContentTypes.Should().Equal("application/json", "text/plain", "application/problem+json");
        invalid.Type.Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.Integer);
        invalid.MediaTypes.Select(media => media.Name).Should().Equal(invalid.ContentTypes);
        invalid.MediaTypes[0].Type.Should().BeSameAs(invalid.Type);
        invalid.MediaTypes[1].Type.Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.String);
        invalid.MediaTypes[2].Type.Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("ReportProblem");
    }
}
