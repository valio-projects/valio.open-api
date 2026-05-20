using FluentAssertions;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Parsing;

/// <summary>
/// Edge cases for the OpenAPI document parser.
/// </summary>
[TestFixture]
public class ParserEdgeCasesTests
{
    [Test]
    public async Task Petstore_HasAtLeastOnePathWithMultipleOperations()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        // /pet path has POST, PUT in Petstore 3.0.
        doc.Paths!["/pet"].Operations.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Petstore_ResolvesNestedReferences()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var pet = doc.Components!.Schemas["Pet"];
        pet.Should().NotBeNull();
        // Pet references Category and Tag.
        pet!.Properties.Should().ContainKey("category");
        pet.Properties.Should().ContainKey("tags");
    }

    [Test]
    public async Task ComplexSchema_AnimalOneOfHasDiscriminator()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("complex-schema.json");
        var animal = doc.Components!.Schemas["Animal"];
        animal!.Discriminator.Should().NotBeNull();
        animal.Discriminator!.PropertyName.Should().Be("kind");
        animal.Discriminator.Mapping.Should().ContainKeys("dog", "cat", "bird");
    }

    [Test]
    public async Task ComplexSchema_TreeNode_ChildrenIsArrayOfTreeNode()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("complex-schema.json");
        var tree = doc.Components!.Schemas["TreeNode"];
        var children = tree!.Properties["children"];
        children.Type?.ToString().Should().ContainEquivalentOf("array");
        // Items should resolve to TreeNode (self-reference).
        children.Items.Should().NotBeNull();
    }

    [Test]
    public async Task SimpleSchema_OperationIds_ArePresent()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var op = doc.Paths!["/users"].Operations.First().Value;
        op.OperationId.Should().Be("getUsers");
    }

    [Test]
    public async Task Petstore_OperationsTagged()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var addPet = doc.Paths!["/pet"].Operations[HttpMethod.Post];
        addPet.Tags.Should().NotBeNullOrEmpty();
    }
}
