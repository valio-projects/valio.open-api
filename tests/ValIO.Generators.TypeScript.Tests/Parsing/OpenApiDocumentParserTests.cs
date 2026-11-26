using ValIO.Tests.Fixtures;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Parsing;

/// <summary>
/// Tests for OpenAPI document loading. Uses the PipelineTestHarness
/// embedded-resource loader, which exercises the Microsoft.OpenApi
/// parser used everywhere in ValIO.
/// </summary>
[TestFixture]
public class OpenApiDocumentParserTests
{
    [Test]
    public async Task SimpleSchema_LoadsRootInfo()
    {
        var doc = await OpenApiFixture.LoadAsync("Examples/simple-schema.json");
        doc.Should().NotBeNull();
        doc.Info.Should().NotBeNull();
        doc.Info.Title.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task SimpleSchema_ResolvesSchemaReferences()
    {
        var doc = await OpenApiFixture.LoadAsync("Examples/simple-schema.json");
        doc.Components!.Schemas.Should().ContainKey("User");
        doc.Components.Schemas.Should().ContainKey("UserRole");
    }

    [Test]
    public async Task PetstoreSchema_LoadsAllPaths()
    {
        var doc = await OpenApiFixture.LoadAsync("Examples/petstore.json");
        doc.Paths.Should().NotBeEmpty();
        doc.Paths!.Should().ContainKey("/pet");
    }

    [Test]
    public async Task ComplexSchema_PreservesAllOfComposition()
    {
        var doc = await OpenApiFixture.LoadAsync("TypeScript/complex-schema.json");
        var dog = doc.Components!.Schemas["Dog"];
        dog.Should().NotBeNull();
        // dog uses allOf; verify the parser preserved that composition (.AllOf is non-null).
        dog!.AllOf.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task ComplexSchema_PreservesOneOfComposition()
    {
        var doc = await OpenApiFixture.LoadAsync("TypeScript/complex-schema.json");
        var animal = doc.Components!.Schemas["Animal"];
        animal!.OneOf.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task ComplexSchema_RecursiveTreeNodeReferenceLoads()
    {
        var doc = await OpenApiFixture.LoadAsync("TypeScript/complex-schema.json");
        var tree = doc.Components!.Schemas["TreeNode"];
        tree.Should().NotBeNull();
        tree!.Properties.Should().ContainKey("children");
    }
}
