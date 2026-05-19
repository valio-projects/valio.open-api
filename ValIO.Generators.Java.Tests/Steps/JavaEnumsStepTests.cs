using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Java.Tests.TestHelpers;

namespace ValIO.Generators.Java.Tests.Steps;

[TestFixture]
public class JavaEnumsStepTests
{
    private JavaPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new JavaPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task EmitsBackedEnumWithValueConstructor()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/UserRole.java", StringComparison.Ordinal));
        enumFile.Content.Should().Contain("public enum UserRole {");
        enumFile.Content.Should().Contain("ADMIN(\"admin\")");
        enumFile.Content.Should().Contain("USER(\"user\")");
        enumFile.Content.Should().Contain("GUEST(\"guest\")");
        enumFile.Content.Should().Contain("private final String value;");
        enumFile.Content.Should().Contain("public String value()");
    }

    [Test]
    public async Task EnumFiles_LiveAlongsideModels()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().Contain(f =>
            f.RelativePath.Replace('\\', '/') == "models/UserRole.java");
    }

    [Test]
    public async Task EnumConstants_AreUppercaseSnakeCase()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/UserRole.java", StringComparison.Ordinal));
        // Lowercase tokens "admin"/"user"/"guest" become ADMIN/USER/GUEST.
        enumFile.Content.Should().Contain("ADMIN(\"admin\")");
        enumFile.Content.Should().Contain("USER(\"user\")");
        enumFile.Content.Should().Contain("GUEST(\"guest\")");
    }
}
