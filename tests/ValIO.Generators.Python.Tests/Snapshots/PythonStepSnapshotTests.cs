using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Python.Options;
using ValIO.Generators.Python.Tests.TestHelpers;

namespace ValIO.Generators.Python.Tests.Snapshots;

/// <summary>
/// Golden-file snapshot tests for Python pipeline output.
/// First run writes the snapshot. Set <c>UPDATE_SNAPSHOTS=1</c> to regenerate.
/// </summary>
[TestFixture]
public class PythonStepSnapshotTests
{
    private PythonPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PythonPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task UserModel_MatchesSnapshot()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
            && f.RelativePath.Contains("user", StringComparison.OrdinalIgnoreCase)
            && !f.RelativePath.Contains("role", StringComparison.OrdinalIgnoreCase));
        Snapshot.Match(userFile.Content, "PydanticModel/user");
    }

    [Test]
    public async Task UserRoleEnum_MatchesSnapshot()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var roleFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
            && f.RelativePath.Contains("user_role", StringComparison.OrdinalIgnoreCase));
        Snapshot.Match(roleFile.Content, "PythonEnum/user_role");
    }
}
