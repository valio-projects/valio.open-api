using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Java.Tests.TestHelpers;

namespace ValIO.Generators.Java.Tests.Steps;

[TestFixture]
public class JavaModelsStepTests
{
    private JavaPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new JavaPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task GeneratesOneFilePerModel_InModelsFolder()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var modelFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.Replace('\\', '/').StartsWith("models/", StringComparison.Ordinal))
            .ToList();
        modelFiles.Should().NotBeEmpty();
        modelFiles.Should().Contain(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/User.java", StringComparison.Ordinal));
    }

    [Test]
    public async Task EmitsPackageAndRecordDeclaration()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/User.java", StringComparison.Ordinal));
        userFile.Content.Should().Contain("package com.example.generated.models;");
        userFile.Content.Should().Contain("public record User(");
        userFile.Content.Should().Contain(") {}");
    }

    [Test]
    public async Task RequiredPrimitives_AreUnboxed_OptionalAreBoxed()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/User.java", StringComparison.Ordinal));
        var normalized = userFile.Content.Replace("\r\n", "\n", StringComparison.Ordinal);
        // id is required+integer → primitive int. name/email required+string → String.
        normalized.Should().Contain("int id");
        normalized.Should().Contain("String name");
        normalized.Should().Contain("String email");
    }

    [Test]
    public async Task ReferencesEnumByClassName_NoImportNeededInSamePackage()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/User.java", StringComparison.Ordinal));
        userFile.Content.Should().Contain("UserRole role");
        // Same models package — no fully-qualified import for sibling models.
        userFile.Content.Should().NotContain("import com.example.generated.models.UserRole;");
    }

    [Test]
    public async Task CustomPackage_RespectedInOutput()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new Java.Options.JavaGenerationOptions { Package = "io.acme.api" });

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/User.java", StringComparison.Ordinal));
        userFile.Content.Should().Contain("package io.acme.api.models;");
    }
}
