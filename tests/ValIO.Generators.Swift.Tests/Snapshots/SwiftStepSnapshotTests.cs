using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Swift.Options;
using ValIO.Generators.Swift.Tests.TestHelpers;

namespace ValIO.Generators.Swift.Tests.Snapshots;

[TestFixture]
public class SwiftStepSnapshotTests
{
    private SwiftPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new SwiftPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task UserStruct_MatchesSnapshot()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.swift", StringComparison.OrdinalIgnoreCase));
        Snapshot.Match(userFile.Content, "SwiftStruct/User");
    }

    [Test]
    public async Task UserRoleEnum_MatchesSnapshot()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var roleFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("UserRole.swift", StringComparison.OrdinalIgnoreCase));
        Snapshot.Match(roleFile.Content, "SwiftEnum/UserRole");
    }
}
