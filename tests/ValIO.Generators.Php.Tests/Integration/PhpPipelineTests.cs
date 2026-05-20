using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.Php.Tests.TestHelpers;

namespace ValIO.Generators.Php.Tests.Integration;

/// <summary>
/// Integration tests for the PHP plugin pipeline.
/// </summary>
[TestFixture]
public class PhpPipelineTests
{
    private PhpPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PhpPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task SimpleSchema_GeneratesPhpFiles()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().NotBeEmpty();
        ctx.GeneratedFiles.Select(f => f.RelativePath).Should().Contain(p => p.EndsWith(".php"));
    }

    [Test]
    public async Task UserModel_GeneratesPhpClassWithStrictTypes()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').Contains("User", StringComparison.OrdinalIgnoreCase)
            && f.RelativePath.EndsWith(".php", StringComparison.OrdinalIgnoreCase));
        userFile.Should().NotBeNull();
        userFile!.Content.Should().Contain("<?php");
        userFile.Content.Should().Contain("declare(strict_types=1)");
        userFile.Content.Should().Contain("class User");
    }

    [Test]
    public async Task UserModel_HasReadonlyConstructorPromotion()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("User", StringComparison.OrdinalIgnoreCase)
            && f.RelativePath.EndsWith(".php", StringComparison.OrdinalIgnoreCase));
        userFile.Content.Should().Contain("public readonly");
        userFile.Content.Should().Contain("__construct");
    }

    [Test]
    public async Task EnumFile_GeneratesPhp81BackedEnum()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.Content.Contains("enum UserRole", StringComparison.Ordinal));
        enumFile.Should().NotBeNull();
        enumFile!.Content.Should().Contain("enum UserRole");
    }

    [Test]
    public async Task ClientFile_UsesGuzzle()
    {
        var doc = await PhpPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.Content.Contains("GuzzleHttp", StringComparison.Ordinal));
        clientFile.Should().NotBeNull();
    }

    [Test]
    public async Task GeneratorRegistry_FindsPhpByAlias()
    {
        await Task.CompletedTask;
        var registry = _harness.Services.GetService(typeof(IGeneratorRegistry)) as IGeneratorRegistry;
        registry.Should().NotBeNull();
        registry!.FindPlugin("php").Should().NotBeNull();
        registry.FindPlugin("PHP").Should().NotBeNull();
        registry.FindPlugin("php8").Should().NotBeNull();
    }
}
