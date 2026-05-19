using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Kotlin.Tests.TestHelpers;

namespace ValIO.Generators.Kotlin.Tests.Steps;

[TestFixture]
public class KotlinModelsStepTests
{
    private KotlinPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new KotlinPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task GeneratesOneFilePerModel_UnderModelsPackage()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var modelFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.Replace('\\', '/').Contains("/models/", StringComparison.Ordinal))
            .ToList();
        modelFiles.Should().NotBeEmpty();
        modelFiles.Should().Contain(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/User.kt", StringComparison.Ordinal));
    }

    [Test]
    public async Task ModelEmitsDataClassWithSerializableAnnotation()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/User.kt", StringComparison.Ordinal));
        userFile.Content.Should().Contain("package com.example.generated.models");
        userFile.Content.Should().Contain("import kotlinx.serialization.Serializable");
        userFile.Content.Should().Contain("@Serializable");
        userFile.Content.Should().Contain("data class User(");
    }

    [Test]
    public async Task RequiredFieldsHaveNoDefault_OptionalGetNullDefault()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/User.kt", StringComparison.Ordinal));
        var normalized = userFile.Content.Replace("\r\n", "\n", StringComparison.Ordinal);
        normalized.Should().Contain("val id: Int,");
        normalized.Should().Contain("val name: String,");
        normalized.Should().Contain("val email: String,");
        normalized.Should().Contain("val role: UserRole? = null,");
    }

    [Test]
    public async Task ReferencedModelsAreImportedFromTheSamePackage()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/User.kt", StringComparison.Ordinal));
        userFile.Content.Should().Contain("import com.example.generated.models.UserRole");
    }

    [Test]
    public async Task CustomPackage_FlowsThroughToBothFilesAndDeclarations()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new Options.KotlinGenerationOptions { Package = "com.acme.gen" });

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/User.kt", StringComparison.Ordinal));
        userFile.RelativePath.Replace('\\', '/').Should().StartWith("com/acme/gen/models/");
        userFile.Content.Should().Contain("package com.acme.gen.models");
    }
}
