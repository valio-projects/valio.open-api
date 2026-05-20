using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Php.Tests.TestHelpers;

namespace ValIO.Generators.Php.Tests.Integration;

/// <summary>
/// File-count + content checks for the PHP pipeline on Petstore.
/// </summary>
[TestFixture]
public class PhpPetstoreFileCountTests
{
    private PhpPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PhpPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task Petstore_GeneratesAllExpectedModels()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var expected = new[] { "Pet.php", "Category.php", "Tag.php", "Order.php", "User.php", "ApiResponse.php" };
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
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles)
            f.Content.Should().NotBeNullOrWhiteSpace(f.RelativePath);
    }

    [Test]
    public async Task EveryPhpFile_StartsWithPhpTag()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".php")))
            f.Content.TrimStart().Should().StartWith("<?php", $"{f.RelativePath} missing <?php tag");
    }
}
