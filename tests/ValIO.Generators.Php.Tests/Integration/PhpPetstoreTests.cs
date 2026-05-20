using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Php.Tests.TestHelpers;

namespace ValIO.Generators.Php.Tests.Integration;

[TestFixture]
public class PhpPetstoreTests
{
    private PhpPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PhpPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task Petstore_GeneratesModelsForCoreSchemas()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Contains("Pet", StringComparison.OrdinalIgnoreCase));
        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Contains("User", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Petstore_ModelsHaveReadonlyConstructorPromotion()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var petFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".php", StringComparison.OrdinalIgnoreCase)
            && f.RelativePath.Contains("Pet", StringComparison.OrdinalIgnoreCase));
        petFile.Content.Should().Contain("class Pet");
        petFile.Content.Should().Contain("readonly");
    }

    [Test]
    public async Task Petstore_ClientUsesGuzzle()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.Content.Contains("Guzzle", StringComparison.Ordinal));
        clientFile.Should().NotBeNull();
    }
}
