using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Php.Tests.TestHelpers;

namespace ValIO.Generators.Php.Tests.Integration;

/// <summary>
/// Invariants every emitted PHP file must satisfy.
/// </summary>
[TestFixture]
public class PhpInvariantsTests
{
    private PhpPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PhpPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task EveryPhpFile_StartsWithOpenTag()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f =>
            f.RelativePath.EndsWith(".php", StringComparison.OrdinalIgnoreCase)))
        {
            f.Content.TrimStart().Should().StartWith("<?php",
                $"file {f.RelativePath} must begin with <?php tag");
        }
    }

    [Test]
    public async Task EveryPhpFile_HasNamespaceDeclaration()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f =>
            f.RelativePath.EndsWith(".php", StringComparison.OrdinalIgnoreCase)))
        {
            f.Content.Should().Contain("namespace ",
                $"file {f.RelativePath} should declare a namespace");
        }
    }

    [Test]
    public async Task ModelFile_FinalReadonlyClass()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("User.php", StringComparison.OrdinalIgnoreCase));
        userFile.Content.Should().Contain("final class User");
    }

    [Test]
    public async Task EveryPhpFile_BalancedBraces()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f =>
            f.RelativePath.EndsWith(".php", StringComparison.OrdinalIgnoreCase)))
        {
            var stripped = System.Text.RegularExpressions.Regex.Replace(f.Content, @"""([^""\\]|\\.)*""", "\"\"");
            stripped = System.Text.RegularExpressions.Regex.Replace(stripped, @"'([^'\\]|\\.)*'", "''");
            var open = stripped.Count(c => c == '{');
            var close = stripped.Count(c => c == '}');
            open.Should().Be(close, $"file {f.RelativePath} has mismatched braces");
        }
    }
}
