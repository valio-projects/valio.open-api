using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Kotlin.Options;
using ValIO.Generators.Kotlin.Tests.TestHelpers;

namespace ValIO.Generators.Kotlin.Tests.Snapshots;

[TestFixture]
public class KotlinStepSnapshotTests
{
    private KotlinPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new KotlinPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task UserModel_MatchesSnapshot()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.kt", StringComparison.OrdinalIgnoreCase));
        Snapshot.Match(userFile.Content, "KotlinDataClass/User");
    }

    [Test]
    public async Task UserRoleEnum_MatchesSnapshot()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var roleFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("UserRole.kt", StringComparison.OrdinalIgnoreCase));
        Snapshot.Match(roleFile.Content, "KotlinEnum/UserRole");
    }
}
