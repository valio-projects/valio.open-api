using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Kotlin.Tests.TestHelpers;

namespace ValIO.Generators.Kotlin.Tests.Integration;

/// <summary>
/// Cross-cutting invariants that every emitted Kotlin file must satisfy.
/// </summary>
[TestFixture]
public class KotlinInvariantsTests
{
    private KotlinPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new KotlinPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task EveryKtFile_StartsWithPackage()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f =>
            f.RelativePath.EndsWith(".kt", StringComparison.OrdinalIgnoreCase)))
        {
            f.Content.TrimStart().Should().StartWith("package ",
                $"file {f.RelativePath} must start with package declaration");
        }
    }

    [Test]
    public async Task EveryKtFile_BalancedBraces()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f =>
            f.RelativePath.EndsWith(".kt", StringComparison.OrdinalIgnoreCase)))
        {
            var stripped = System.Text.RegularExpressions.Regex.Replace(f.Content, @"""([^""\\]|\\.)*""", "\"\"");
            var open = stripped.Count(c => c == '{');
            var close = stripped.Count(c => c == '}');
            open.Should().Be(close, $"file {f.RelativePath} has mismatched braces");
        }
    }

    [Test]
    public async Task ModelFile_DataClassOrRegular()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.kt", StringComparison.OrdinalIgnoreCase));
        // Either data class (most common) or class.
        var ok = userFile.Content.Contains("data class") || userFile.Content.Contains("class User");
        ok.Should().BeTrue();
    }

    [Test]
    public async Task EnumFile_DeclaresEnumClass()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("UserRole.kt", StringComparison.OrdinalIgnoreCase));
        enumFile.Content.Should().Contain("enum class");
    }
}
