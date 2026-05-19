using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Swift.Options;
using ValIO.Generators.Swift.Tests.TestHelpers;

namespace ValIO.Generators.Swift.Tests.Steps;

[TestFixture]
public class SwiftModelsStepTests
{
    private SwiftPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new SwiftPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task GeneratesOneFilePerModel_UnderModelsDir()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var modelFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.Replace('\\', '/').Contains("/Models/", StringComparison.Ordinal))
            .ToList();
        modelFiles.Should().NotBeEmpty();
        modelFiles.Should().Contain(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("Models/User.swift", StringComparison.Ordinal));
    }

    [Test]
    public async Task ModelEmitsPublicStructWithCodableAndSendable()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("Models/User.swift", StringComparison.Ordinal));
        userFile.Content.Should().Contain("import Foundation");
        userFile.Content.Should().Contain("public struct User: Codable, Sendable {");
    }

    [Test]
    public async Task RequiredFieldsHaveNoDefault_OptionalGetNilDefault()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("Models/User.swift", StringComparison.Ordinal));
        var normalized = userFile.Content.Replace("\r\n", "\n", StringComparison.Ordinal);
        normalized.Should().Contain("public let id: Int");
        normalized.Should().Contain("public let name: String");
        normalized.Should().Contain("public let email: String");
        normalized.Should().Contain("public let role: UserRole? = nil");
    }

    [Test]
    public async Task CustomModuleName_FlowsThroughToPaths()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new SwiftGenerationOptions { ModuleName = "ApiKit" });

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("Models/User.swift", StringComparison.Ordinal));
        userFile.RelativePath.Replace('\\', '/').Should().StartWith("ApiKit/Models/");
    }
}
