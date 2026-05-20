using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Java.Options;
using ValIO.Generators.Java.Tests.TestHelpers;

namespace ValIO.Generators.Java.Tests.Integration;

[TestFixture]
public class JavaPetstoreTests
{
    private JavaPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new JavaPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task Petstore_GeneratesModelsForCoreSchemas()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Contains("Pet", StringComparison.OrdinalIgnoreCase));
        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Contains("User", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Petstore_ModelsAreJavaRecordsOrClasses()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var anyModel = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".java", StringComparison.OrdinalIgnoreCase)
            && f.RelativePath.Contains("Pet", StringComparison.OrdinalIgnoreCase));
        // Either "record Pet" or "class Pet" depending on Java version target.
        (anyModel.Content.Contains("record Pet") || anyModel.Content.Contains("class Pet"))
            .Should().BeTrue();
    }

    [Test]
    public async Task Petstore_ClientUsesOkHttp()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.Content.Contains("okhttp", StringComparison.OrdinalIgnoreCase));
        clientFile.Should().NotBeNull("at least one file uses OkHttp");
    }
}
