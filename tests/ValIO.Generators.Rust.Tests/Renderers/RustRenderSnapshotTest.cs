using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Rust.Tests.TestHelpers;

namespace ValIO.Generators.Rust.Tests.Renderers;

/// <summary>
/// Smoke tests that print the rendered output for visual sanity checks.
/// </summary>
[TestFixture]
public class RustRenderSnapshotTest
{
    private RustPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new RustPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task UserModelFile_LooksReasonable()
    {
        var doc = await RustPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("user.rs", StringComparison.OrdinalIgnoreCase));
        TestContext.WriteLine(userFile.Content);
        userFile.Content.Should().Contain("use serde::");
        userFile.Content.Should().Contain("pub struct User");
        userFile.Content.Should().Contain("pub id: i32,");
        // Optional field 'role' should be wrapped in Option<UserRole>.
        userFile.Content.Should().Contain("Option<UserRole>");
    }

    [Test]
    public async Task ClientFile_LooksReasonable()
    {
        var doc = await RustPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("/src/api/", StringComparison.OrdinalIgnoreCase));
        TestContext.WriteLine(clientFile.Content);
        clientFile.Content.Should().Contain("pub async fn");
        clientFile.Content.Should().Contain("client.get(");
        clientFile.Content.Should().Contain(".send().await?");
    }
}
