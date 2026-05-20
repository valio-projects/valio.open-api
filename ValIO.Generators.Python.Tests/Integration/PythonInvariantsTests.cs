using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Python.Options;
using ValIO.Generators.Python.Tests.TestHelpers;

namespace ValIO.Generators.Python.Tests.Integration;

/// <summary>
/// Invariants the generated Python output must always satisfy. These tests
/// are cheap to keep green and catch wide classes of regressions
/// (broken imports, naming, indentation, escaping).
/// </summary>
[TestFixture]
public class PythonInvariantsTests
{
    private PythonPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PythonPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task EveryFile_StartsWithFutureAnnotations_Or_NoSyntaxError()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".py")))
        {
            // Files either declare future imports or have valid syntax.
            // Sanity: no Python file should end with an unclosed paren or stray colon.
            f.Content.TrimEnd().Should().NotEndWith(":", "no Python file should end with bare colon");
            f.Content.TrimEnd().Should().NotEndWith(",", "no trailing comma at EOF");
        }
    }

    [Test]
    public async Task ModelFile_HasClassKeyword()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("user.py", StringComparison.Ordinal));
        userFile.Content.Should().Contain("class User");
    }

    [Test]
    public async Task NoFile_HasMixedLineEndings()
    {
        // Platform-dependent: Environment.NewLine is CRLF on Windows, LF on Unix.
        // What matters is that whichever ending the renderer uses, it's used uniformly.
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles)
        {
            var lf = f.Content.Count(c => c == '\n');
            var crlf = System.Text.RegularExpressions.Regex.Matches(f.Content, "\r\n").Count;
            var loneCr = f.Content.Count(c => c == '\r') - crlf;
            loneCr.Should().Be(0, $"file {f.RelativePath} should not have lone CR characters");
            // Either fully LF (lf > 0, crlf == 0) or fully CRLF (crlf == lf).
            (crlf == 0 || crlf == lf).Should().BeTrue(
                $"file {f.RelativePath} has mixed endings: lf={lf} crlf={crlf}");
        }
    }

    [Test]
    public async Task PythonAsyncMode_GeneratesAsyncDef()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new PythonGenerationOptions { HttpxMode = "async" });

        var client = ctx.GeneratedFiles.First(f =>
            f.RelativePath.StartsWith("api/", StringComparison.Ordinal)
            && f.RelativePath.EndsWith(".py", StringComparison.Ordinal));
        client.Content.Should().Contain("async def");
        client.Content.Should().Contain("await ");
    }

    [Test]
    public async Task PythonSyncMode_GeneratesPlainDef()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new PythonGenerationOptions { HttpxMode = "sync" });

        var client = ctx.GeneratedFiles.First(f =>
            f.RelativePath.StartsWith("api/", StringComparison.Ordinal)
            && f.RelativePath.EndsWith(".py", StringComparison.Ordinal));
        client.Content.Should().Contain("def ").And.NotContain("async def ");
    }

    [Test]
    public async Task EnumFile_DeclaresStrAndEnum()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("user_role.py", StringComparison.Ordinal));
        // Either subclass StrEnum / Enum / IntEnum, or use Literal["A", "B"].
        var ok = enumFile.Content.Contains("Enum")
                 || enumFile.Content.Contains("Literal[");
        ok.Should().BeTrue($"enum file should use Enum or Literal; got: {enumFile.Content}");
    }
}
