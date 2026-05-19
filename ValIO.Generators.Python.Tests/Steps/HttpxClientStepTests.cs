using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Python.Options;
using ValIO.Generators.Python.Tests.TestHelpers;

namespace ValIO.Generators.Python.Tests.Steps;

[TestFixture]
public class HttpxClientStepTests
{
    private PythonPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PythonPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task EmitsAsyncClassWithHttpx_ByDefault()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.Replace('\\', '/').StartsWith("api/", StringComparison.Ordinal))
            .ToList();
        clientFiles.Should().NotBeEmpty();

        var content = clientFiles[0].Content;
        content.Should().Contain("import httpx");
        content.Should().Contain("httpx.AsyncClient");
        content.Should().Contain("async def ");
        content.Should().Contain("await self._client.");
    }

    [Test]
    public async Task UsesSnakeCaseMethodNames_FromOperationId()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').StartsWith("api/", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("async def get_users(");
        clientFile.Content.Should().Contain("async def get_user_by_id(");
        clientFile.Content.Should().NotContain("async def getUsers");
        clientFile.Content.Should().NotContain("async def getUserById");
    }

    [Test]
    public async Task RaisesForStatus_AndCallsModelValidate_ForPydanticReturn()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').StartsWith("api/", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("response.raise_for_status()");
        clientFile.Content.Should().Contain("User.model_validate(response.json())");
    }

    [Test]
    public async Task PathParamsRenderedAsFStrings()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').StartsWith("api/", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("f\"/users/{id}\"");
    }

    [Test]
    public async Task SyncMode_UsesHttpxClient_AndDoesNotEmitAsyncKeyword()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new PythonGenerationOptions { HttpxMode = PythonOptionIds.HttpxMode.Sync });

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').StartsWith("api/", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("httpx.Client");
        clientFile.Content.Should().NotContain("async def ");
        clientFile.Content.Should().NotContain("await ");
    }
}
