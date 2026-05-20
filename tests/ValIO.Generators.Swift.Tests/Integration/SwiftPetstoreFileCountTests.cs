using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Swift.Tests.TestHelpers;

namespace ValIO.Generators.Swift.Tests.Integration;

/// <summary>
/// File-count + content checks for the Swift pipeline on Petstore.
/// </summary>
[TestFixture]
public class SwiftPetstoreFileCountTests
{
    private SwiftPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new SwiftPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task Petstore_GeneratesAllExpectedModels()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var expected = new[] { "Pet.swift", "Category.swift", "Tag.swift", "Order.swift", "User.swift", "ApiResponse.swift" };
        foreach (var name in expected)
        {
            ctx.GeneratedFiles.Should().Contain(
                f => f.RelativePath.EndsWith(name, StringComparison.OrdinalIgnoreCase),
                $"expected model file {name}");
        }
    }

    [Test]
    public async Task EveryFile_HasNonEmptyContent()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles)
            f.Content.Should().NotBeNullOrWhiteSpace(f.RelativePath);
    }

    [Test]
    public async Task EverySwiftFile_HasNoTabs()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".swift")))
            f.Content.Should().NotContain("\t", $"file {f.RelativePath} has tabs");
    }

    [Test]
    public async Task Petstore_PetStruct_ReferencesCategoryAndTag()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var pet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("Pet.swift", StringComparison.OrdinalIgnoreCase));
        pet.Content.Should().Contain("Category");
        pet.Content.Should().Contain("Tag");
    }
}
