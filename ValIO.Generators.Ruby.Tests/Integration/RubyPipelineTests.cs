using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Ruby.Tests.TestHelpers;

namespace ValIO.Generators.Ruby.Tests.Integration;

/// <summary>
/// Integration tests for the full Ruby pipeline.
/// </summary>
[TestFixture]
public class RubyPipelineTests
{
    private RubyPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new RubyPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task SimpleSchema_ProducesAtLeastOneModelFile()
    {
        var doc = await RubyPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().NotBeEmpty();
        ctx.GeneratedFiles.Select(f => f.RelativePath).Should().Contain(p => p.EndsWith(".rb"));
        ctx.GeneratedFiles.Select(f => f.RelativePath).Should().Contain(p => p.Contains("/models/"));
    }

    [Test]
    public async Task SimpleSchema_EveryGeneratedFileIsNonEmpty()
    {
        var doc = await RubyPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().NotBeEmpty();
        foreach (var file in ctx.GeneratedFiles)
        {
            file.Content.Should().NotBeNullOrWhiteSpace($"{file.RelativePath} should have content");
        }
    }

    [Test]
    public async Task SimpleSchema_EveryRubyFileContainsClassKeyword()
    {
        var doc = await RubyPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var rubyFiles = ctx.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".rb")).ToList();
        rubyFiles.Should().NotBeEmpty();
        foreach (var file in rubyFiles)
        {
            file.Content.Should().Contain("class ",
                $"{file.RelativePath} should declare at least one class");
        }
    }

    [Test]
    public async Task SimpleSchema_EveryRubyFileStartsWithFrozenStringLiteralPragma()
    {
        var doc = await RubyPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var rubyFiles = ctx.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".rb")).ToList();
        rubyFiles.Should().NotBeEmpty();
        foreach (var file in rubyFiles)
        {
            file.Content.Should().StartWith("# frozen_string_literal: true",
                $"{file.RelativePath} should begin with the frozen string literal pragma");
        }
    }
}
