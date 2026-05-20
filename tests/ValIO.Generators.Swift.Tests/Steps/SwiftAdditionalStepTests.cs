using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Swift.Options;
using ValIO.Generators.Swift.Tests.TestHelpers;

namespace ValIO.Generators.Swift.Tests.Steps;

[TestFixture]
public class SwiftAdditionalStepTests
{
    private SwiftPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new SwiftPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task ModelFile_ImportsFoundation()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.swift", StringComparison.OrdinalIgnoreCase));
        userFile.Content.Should().Contain("import Foundation");
    }

    [Test]
    public async Task ModelFile_StructIsPublicAndSendable()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.swift", StringComparison.OrdinalIgnoreCase));
        userFile.Content.Should().Contain("public struct User");
        userFile.Content.Should().Contain("Sendable");
    }

    [Test]
    public async Task EnumFile_HasStringRawTypeAndCases()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("UserRole.swift", StringComparison.OrdinalIgnoreCase));
        enumFile.Content.Should().Contain("public enum UserRole: String");
        enumFile.Content.Should().Contain("case admin");
    }

    [Test]
    public async Task ClientFile_UsesAsyncThrows()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".swift", StringComparison.OrdinalIgnoreCase)
            && f.Content.Contains("URLSession", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("async throws");
    }
}
