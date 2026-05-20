using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Python.Tests.TestHelpers;

namespace ValIO.Generators.Python.Tests.Integration;

/// <summary>
/// Additional Petstore integration checks for the Python pipeline:
/// file-count invariants and per-model content checks.
/// </summary>
[TestFixture]
public class PythonPetstoreFileCountTests
{
    private PythonPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PythonPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task Petstore_GeneratesAllSixModels()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        // Six Petstore models: Pet, Category, Tag, Order, User, ApiResponse.
        var expected = new[] { "pet.py", "category.py", "tag.py", "order.py", "user.py", "api_response.py" };
        foreach (var name in expected)
        {
            ctx.GeneratedFiles.Should().Contain(
                f => f.RelativePath.Replace('\\', '/').EndsWith("/" + name, StringComparison.OrdinalIgnoreCase),
                $"expected model file {name}");
        }
    }

    [Test]
    public async Task Petstore_ClientFilesGenerated_AtLeastOnePerTag()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        // Petstore has 3 tags (pet/store/user). Python groups by tag,
        // so expect ≥ 3 client files.
        clientFiles.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task EveryGeneratedFile_HasNonEmptyContent()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles)
            f.Content.Should().NotBeNullOrWhiteSpace($"file {f.RelativePath} must have content");
    }

    [Test]
    public async Task Petstore_PetModel_ReferencesCategoryAndTag()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc);

        var pet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("/pet.py", StringComparison.OrdinalIgnoreCase));
        pet.Content.Should().Contain("Category");
        pet.Content.Should().Contain("Tag");
    }
}
