using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Java.Tests.TestHelpers;

namespace ValIO.Generators.Java.Tests.Integration;

/// <summary>
/// Cross-cutting file-count + content checks for the Java pipeline on Petstore.
/// </summary>
[TestFixture]
public class JavaPetstoreFileCountTests
{
    private JavaPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new JavaPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task Petstore_GeneratesAllExpectedModelFiles()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var expected = new[] { "Pet.java", "Category.java", "Tag.java", "Order.java", "User.java", "ApiResponse.java" };
        foreach (var name in expected)
        {
            ctx.GeneratedFiles.Should().Contain(
                f => f.RelativePath.EndsWith(name, StringComparison.OrdinalIgnoreCase),
                $"expected model file {name}");
        }
    }

    [Test]
    public async Task EveryGeneratedFile_HasNonEmptyContent()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles)
            f.Content.Should().NotBeNullOrWhiteSpace($"file {f.RelativePath} must have content");
    }

    [Test]
    public async Task EveryJavaFile_StartsWithPackage()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".java")))
            f.Content.TrimStart().Should().StartWith("package ", $"file {f.RelativePath}");
    }

    [Test]
    public async Task Petstore_PetClass_ReferencesCategoryAndTag()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var pet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("Pet.java", StringComparison.OrdinalIgnoreCase));
        pet.Content.Should().Contain("Category");
        pet.Content.Should().Contain("Tag");
    }
}
