using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Kotlin.Tests.TestHelpers;

namespace ValIO.Generators.Kotlin.Tests.Steps;

[TestFixture]
public class KotlinEnumsStepTests
{
    private KotlinPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new KotlinPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task StringEnum_GeneratesSerializableEnumClass()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/UserRole.kt", StringComparison.Ordinal));
        enumFile.Content.Should().Contain("import kotlinx.serialization.SerialName");
        enumFile.Content.Should().Contain("import kotlinx.serialization.Serializable");
        enumFile.Content.Should().Contain("@Serializable");
        enumFile.Content.Should().Contain("enum class UserRole {");
    }

    [Test]
    public async Task StringEnumMembers_GetSerialNameAndUppercaseIdentifier()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/UserRole.kt", StringComparison.Ordinal));
        enumFile.Content.Should().Contain("@SerialName(\"admin\") ADMIN,");
        enumFile.Content.Should().Contain("@SerialName(\"user\") USER,");
        enumFile.Content.Should().Contain("@SerialName(\"guest\") GUEST,");
    }

    [Test]
    public async Task EnumFile_LivesAlongsideModels()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().Contain(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/UserRole.kt", StringComparison.Ordinal));
    }
}
