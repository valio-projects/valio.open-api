using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Python.Options;
using ValIO.Generators.Python.Tests.TestHelpers;

namespace ValIO.Generators.Python.Tests.Integration;

/// <summary>
/// End-to-end smoke tests for the Python pipeline against the canonical
/// Swagger Petstore.
/// </summary>
[TestFixture]
public class PythonPetstoreTests
{
    private PythonPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PythonPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task Petstore_GeneratesAllExpectedModels()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        // Models present: Pet, Category, Tag, Order, User, ApiResponse.
        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Contains("Pet", StringComparison.OrdinalIgnoreCase));
        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Contains("Order", StringComparison.OrdinalIgnoreCase));
        ctx.GeneratedFiles.Should().Contain(f => f.RelativePath.Contains("User", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Petstore_ModelsImportPydanticBaseModel()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var anyModel = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Contains("Pet", StringComparison.OrdinalIgnoreCase)
            && f.RelativePath.EndsWith(".py"));
        anyModel.Content.Should().Contain("BaseModel");
    }

    [Test]
    public async Task Petstore_ClientImportsHttpxAsyncClient()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var anyClient = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.Content.Contains("httpx", StringComparison.Ordinal));
        anyClient.Should().NotBeNull();
    }
}
