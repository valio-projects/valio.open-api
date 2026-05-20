using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Php.Tests.TestHelpers;

namespace ValIO.Generators.Php.Tests.Snapshots;

[TestFixture]
public class PhpStepSnapshotTests
{
    private PhpPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PhpPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task UserClass_MatchesSnapshot()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.php", StringComparison.OrdinalIgnoreCase));
        Snapshot.Match(userFile.Content, "PhpClass/User");
    }

    [Test]
    public async Task UserRoleEnum_MatchesSnapshot()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("UserRole.php", StringComparison.OrdinalIgnoreCase));
        Snapshot.Match(enumFile.Content, "PhpEnum/UserRole");
    }
}
