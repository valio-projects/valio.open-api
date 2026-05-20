using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Swift.Tests.TestHelpers;

namespace ValIO.Generators.Swift.Tests.Integration;

/// <summary>
/// Cross-cutting invariants every emitted Swift file must satisfy.
/// </summary>
[TestFixture]
public class SwiftInvariantsTests
{
    private SwiftPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new SwiftPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task EverySwiftFile_HasNoTabsInsideIndentation()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f =>
            f.RelativePath.EndsWith(".swift", StringComparison.OrdinalIgnoreCase)))
        {
            // SwiftLint default config flags tabs in indentation.
            f.Content.Should().NotContain("\t", $"file {f.RelativePath} should not contain tabs");
        }
    }

    [Test]
    public async Task EverySwiftFile_BalancedBraces()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f =>
            f.RelativePath.EndsWith(".swift", StringComparison.OrdinalIgnoreCase)))
        {
            var stripped = System.Text.RegularExpressions.Regex.Replace(f.Content, @"""([^""\\]|\\.)*""", "\"\"");
            var open = stripped.Count(c => c == '{');
            var close = stripped.Count(c => c == '}');
            open.Should().Be(close, $"file {f.RelativePath} has mismatched braces");
        }
    }

    [Test]
    public async Task ModelFile_StructWithCodable()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.swift", StringComparison.OrdinalIgnoreCase));
        userFile.Content.Should().Contain("struct User");
        userFile.Content.Should().Contain("Codable");
    }

    [Test]
    public async Task EnumFile_HasEnumWithCodable()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("UserRole.swift", StringComparison.OrdinalIgnoreCase));
        enumFile.Content.Should().Contain("enum UserRole");
    }
}
