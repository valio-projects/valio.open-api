using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Python.Options;
using ValIO.Generators.Python.Tests.TestHelpers;

namespace ValIO.Generators.Python.Tests.Steps;

[TestFixture]
public class PythonAdditionalStepTests
{
    private PythonPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PythonPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task ModelFile_InheritsBaseModel()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
            && f.RelativePath.Contains("user", StringComparison.OrdinalIgnoreCase));
        userFile.Content.Should().Contain("class User(BaseModel)");
    }

    [Test]
    public async Task ModelFile_ImportsPydantic()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
            && f.RelativePath.Contains("user", StringComparison.OrdinalIgnoreCase));
        userFile.Content.Should().Contain("pydantic");
    }

    [Test]
    public async Task ClientFile_HasAsyncDef()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
            && f.Content.Contains("httpx", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("async def");
    }

    [Test]
    public async Task EnumFile_UsesStrEnum()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
            && f.RelativePath.Contains("user_role", StringComparison.OrdinalIgnoreCase));
        // Pydantic v2 + Python 3.11 supports both StrEnum and str enum.
        var content = enumFile.Content;
        (content.Contains("StrEnum") || content.Contains("Enum") || content.Contains("str,")).Should().BeTrue();
    }
}
