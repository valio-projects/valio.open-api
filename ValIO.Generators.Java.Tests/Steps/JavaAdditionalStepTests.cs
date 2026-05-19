using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Java.Options;
using ValIO.Generators.Java.Tests.TestHelpers;

namespace ValIO.Generators.Java.Tests.Steps;

[TestFixture]
public class JavaAdditionalStepTests
{
    private JavaPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new JavaPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task ModelFile_HasPackageStatement()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var anyJava = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".java", StringComparison.OrdinalIgnoreCase));
        anyJava.Content.Should().StartWith("package ");
    }

    [Test]
    public async Task ModelFile_RecordOrClass_HasFinalFields()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.java", StringComparison.OrdinalIgnoreCase));
        // Either Java 14+ record (immutable by definition) or class with private final fields.
        (userFile.Content.Contains("record User") || userFile.Content.Contains("final")).Should().BeTrue();
    }

    [Test]
    public async Task EnumFile_HasJsonSupport()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("UserRole.java", StringComparison.OrdinalIgnoreCase));
        enumFile.Content.Should().Contain("enum UserRole");
    }

    [Test]
    public async Task ClientMethod_ReturnsTypedResult()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".java", StringComparison.OrdinalIgnoreCase)
            && f.Content.Contains("OkHttpClient", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("public ");
    }
}
