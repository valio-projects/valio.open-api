using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Kotlin.Options;
using ValIO.Generators.Kotlin.Tests.TestHelpers;

namespace ValIO.Generators.Kotlin.Tests.Steps;

/// <summary>
/// Additional Step-level invariants that round out the existing fixture set.
/// </summary>
[TestFixture]
public class KotlinAdditionalStepTests
{
    private KotlinPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new KotlinPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task ModelFile_HasSerializableAnnotation()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.kt", StringComparison.OrdinalIgnoreCase));
        userFile.Content.Should().Contain("@Serializable");
    }

    [Test]
    public async Task EnumFile_HasSerialNameAnnotations()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("UserRole.kt", StringComparison.OrdinalIgnoreCase));
        enumFile.Content.Should().Contain("@SerialName");
    }

    [Test]
    public async Task ClientFile_HasSuspendFun()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".kt", StringComparison.OrdinalIgnoreCase)
            && f.Content.Contains("OkHttpClient", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("suspend fun");
    }

    [Test]
    public async Task ClientFile_UsesPackageStatement()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var anyFile = ctx.GeneratedFiles.First(f => f.RelativePath.EndsWith(".kt", StringComparison.OrdinalIgnoreCase));
        anyFile.Content.Should().StartWith("package ");
    }
}
