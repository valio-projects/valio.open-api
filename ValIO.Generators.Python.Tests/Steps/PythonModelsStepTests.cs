using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Python.Tests.TestHelpers;

namespace ValIO.Generators.Python.Tests.Steps;

[TestFixture]
public class PythonModelsStepTests
{
    private PythonPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PythonPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task GeneratesOneFilePerModel_InSnakeCase()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var modelFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.Replace('\\', '/').StartsWith("models/", StringComparison.Ordinal))
            .ToList();
        modelFiles.Should().NotBeEmpty();
        modelFiles.Should().Contain(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/user.py", StringComparison.Ordinal));
    }

    [Test]
    public async Task EmitsPydanticBaseModelWithConfig()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/user.py", StringComparison.Ordinal));
        userFile.Content.Should().Contain("from pydantic import BaseModel, ConfigDict");
        userFile.Content.Should().Contain("class User(BaseModel):");
        userFile.Content.Should().Contain("model_config = ConfigDict(populate_by_name=True)");
    }

    [Test]
    public async Task RequiredFieldsHaveNoDefault_OptionalGetNone()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/user.py", StringComparison.Ordinal));
        var normalized = userFile.Content.Replace("\r\n", "\n", StringComparison.Ordinal);
        // id, name, email — required → нет = None.
        normalized.Should().Contain("id: int\n");
        normalized.Should().Contain("name: str\n");
        normalized.Should().Contain("email: str\n");
        // role — не в required → получает " | None\" = None".
        normalized.Should().Contain("= None");
    }

    [Test]
    public async Task ForwardRefToEnum_QuotedAndImportedUnderTypeChecking()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/user.py", StringComparison.Ordinal));
        userFile.Content.Should().Contain("if TYPE_CHECKING:");
        userFile.Content.Should().Contain("from .user_role import UserRole");
        // role ссылается на UserRole — должно быть квотировано.
        userFile.Content.Should().Contain("role: \"UserRole | None\" = None");
        // Forward refs требуют model_rebuild().
        userFile.Content.Should().Contain("User.model_rebuild()");
    }

    [Test]
    public async Task IncludesFutureAnnotationsImport()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/user.py", StringComparison.Ordinal));
        userFile.Content.Should().StartWith("from __future__ import annotations");
    }
}
