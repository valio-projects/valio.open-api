using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Kotlin.Options;
using ValIO.Generators.Kotlin.Tests.TestHelpers;

namespace ValIO.Generators.Kotlin.Tests.Integration;

[TestFixture]
public class KotlinPetstoreTests
{
    private KotlinPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new KotlinPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task Petstore_GeneratesModelsForCoreSchemas()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Contains("Pet", StringComparison.OrdinalIgnoreCase));
        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Contains("User", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Petstore_ModelsAreDataClassesWithSerializable()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var petFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".kt", StringComparison.OrdinalIgnoreCase)
            && f.RelativePath.Contains("Pet", StringComparison.OrdinalIgnoreCase));
        petFile.Content.Should().Contain("data class Pet");
        petFile.Content.Should().Contain("@Serializable");
    }

    [Test]
    public async Task Petstore_ClientUsesOkHttpWithCoroutines()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.Content.Contains("OkHttpClient", StringComparison.Ordinal));
        clientFile.Should().NotBeNull();
        clientFile!.Content.Should().Contain("suspend fun");
    }
}
