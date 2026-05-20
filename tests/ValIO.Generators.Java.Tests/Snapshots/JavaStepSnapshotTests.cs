using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Java.Options;
using ValIO.Generators.Java.Tests.TestHelpers;

namespace ValIO.Generators.Java.Tests.Snapshots;

[TestFixture]
public class JavaStepSnapshotTests
{
    private JavaPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new JavaPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task UserModel_MatchesSnapshot()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.java", StringComparison.OrdinalIgnoreCase));
        Snapshot.Match(userFile.Content, "JavaModel/User");
    }

    [Test]
    public async Task UserRoleEnum_MatchesSnapshot()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var roleFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("UserRole.java", StringComparison.OrdinalIgnoreCase));
        Snapshot.Match(roleFile.Content, "JavaEnum/UserRole");
    }
}
