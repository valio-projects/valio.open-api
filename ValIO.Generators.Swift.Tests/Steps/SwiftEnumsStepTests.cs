using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Swift.Tests.TestHelpers;

namespace ValIO.Generators.Swift.Tests.Steps;

[TestFixture]
public class SwiftEnumsStepTests
{
    private SwiftPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new SwiftPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task StringEnum_GeneratesPublicEnumWithStringRawTypeAndConformances()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("Models/UserRole.swift", StringComparison.Ordinal));
        enumFile.Content.Should().Contain("import Foundation");
        enumFile.Content.Should().Contain("public enum UserRole: String, Codable, Sendable {");
    }

    [Test]
    public async Task StringEnumMembers_GetLowerCamelCaseCasesWithRawValues()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("Models/UserRole.swift", StringComparison.Ordinal));
        var normalized = enumFile.Content.Replace("\r\n", "\n", StringComparison.Ordinal);
        normalized.Should().Contain("case admin = \"admin\"");
        normalized.Should().Contain("case user = \"user\"");
        normalized.Should().Contain("case guest = \"guest\"");
    }

    [Test]
    public async Task EnumFile_LivesAlongsideModels()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().Contain(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("Models/UserRole.swift", StringComparison.Ordinal));
    }
}
