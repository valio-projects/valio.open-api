using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Python.Tests.TestHelpers;

namespace ValIO.Generators.Python.Tests.Steps;

[TestFixture]
public class PythonEnumsStepTests
{
    private PythonPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PythonPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task StringEnum_GeneratesStrEnumWithUppercaseMembers()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/user_role.py", StringComparison.Ordinal));
        enumFile.Content.Should().Contain("from enum import StrEnum");
        enumFile.Content.Should().Contain("class UserRole(StrEnum):");
        enumFile.Content.Should().Contain("ADMIN = \"admin\"");
        enumFile.Content.Should().Contain("USER = \"user\"");
        enumFile.Content.Should().Contain("GUEST = \"guest\"");
    }

    [Test]
    public async Task EnumFiles_LiveAlongsideModels_InSnakeCase()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().Contain(f =>
            f.RelativePath.Replace('\\', '/') == "models/user_role.py");
    }
}
