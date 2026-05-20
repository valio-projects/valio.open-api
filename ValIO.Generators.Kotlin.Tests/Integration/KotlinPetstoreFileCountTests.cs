using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Kotlin.Tests.TestHelpers;

namespace ValIO.Generators.Kotlin.Tests.Integration;

/// <summary>
/// File-count + content checks for the Kotlin pipeline on Petstore.
/// </summary>
[TestFixture]
public class KotlinPetstoreFileCountTests
{
    private KotlinPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new KotlinPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task Petstore_GeneratesAllExpectedModels()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var expected = new[] { "Pet.kt", "Category.kt", "Tag.kt", "Order.kt", "User.kt", "ApiResponse.kt" };
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
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles)
            f.Content.Should().NotBeNullOrWhiteSpace(f.RelativePath);
    }

    [Test]
    public async Task EveryKtFile_StartsWithPackageOrImport()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".kt")))
        {
            var first = f.Content.TrimStart();
            (first.StartsWith("package ") || first.StartsWith("import "))
                .Should().BeTrue($"{f.RelativePath} should start with package or import");
        }
    }
}
