using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Java.Tests.TestHelpers;

namespace ValIO.Generators.Java.Tests.Integration;

/// <summary>
/// Cross-cutting invariants that every emitted Java file must satisfy.
/// </summary>
[TestFixture]
public class JavaInvariantsTests
{
    private JavaPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new JavaPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task EveryJavaFile_StartsWithPackageDeclaration()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f =>
            f.RelativePath.EndsWith(".java", StringComparison.OrdinalIgnoreCase)))
        {
            f.Content.TrimStart().Should().StartWith("package ",
                $"file {f.RelativePath} must begin with package");
        }
    }

    [Test]
    public async Task EveryJavaFile_BalancedBraces()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f =>
            f.RelativePath.EndsWith(".java", StringComparison.OrdinalIgnoreCase)))
        {
            // Strip string/char literals before counting braces; rough but catches genuine bugs.
            var stripped = System.Text.RegularExpressions.Regex.Replace(f.Content, @"""([^""\\]|\\.)*""", "\"\"");
            var open = stripped.Count(c => c == '{');
            var close = stripped.Count(c => c == '}');
            open.Should().Be(close, $"file {f.RelativePath} has mismatched braces ({open} open / {close} close)");
        }
    }

    [Test]
    public async Task EnumFile_ContainsEnumKeyword()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("UserRole.java", StringComparison.OrdinalIgnoreCase));
        enumFile.Content.Should().Contain("enum UserRole");
    }

    [Test]
    public async Task ModelFile_IsImmutable_RecordOrFinalFields()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.java", StringComparison.OrdinalIgnoreCase));
        var ok = userFile.Content.Contains("record User") || userFile.Content.Contains("final ");
        ok.Should().BeTrue($"User.java must be record or have final fields. Content:\n{userFile.Content}");
    }
}
