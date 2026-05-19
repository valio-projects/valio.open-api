using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Swift.Options;
using ValIO.Generators.Swift.Tests.TestHelpers;

namespace ValIO.Generators.Swift.Tests.Integration;

/// <summary>
/// Integration tests for the full Swift pipeline.
/// </summary>
[TestFixture]
public class SwiftPipelineTests
{
    private SwiftPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new SwiftPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task SimpleSchema_GeneratesAtLeastOneModelClientAndEnum()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().NotBeEmpty();
        ctx.GeneratedFiles.Select(f => f.RelativePath).Should().Contain(p => p.EndsWith(".swift"));
    }

    [Test]
    public async Task ModelFile_ContainsCodableStruct()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("User.swift", StringComparison.OrdinalIgnoreCase));
        userFile.Should().NotBeNull();
        userFile!.Content.Should().Contain("import Foundation");
        userFile.Content.Should().Contain("Codable");
        userFile.Content.Should().Contain("struct User");
    }

    [Test]
    public async Task EnumFile_ContainsStringBackedEnum()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var enumFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("UserRole.swift", StringComparison.OrdinalIgnoreCase));
        enumFile.Should().NotBeNull();
        enumFile!.Content.Should().Contain("enum UserRole");
        enumFile.Content.Should().Contain("String");
    }

    [Test]
    public async Task ClientFile_UsesURLSession()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.EndsWith(".swift", StringComparison.OrdinalIgnoreCase)
            && f.Content.Contains("URLSession", StringComparison.Ordinal));
        clientFile.Should().NotBeNull("at least one file uses URLSession");
        clientFile!.Content.Should().Contain("async throws");
    }

    [Test]
    public async Task GeneratorRegistry_FindsSwiftByAlias()
    {
        await Task.CompletedTask;
        var registry = _harness.Services.GetService(typeof(ValIO.Core.Plugins.IGeneratorRegistry))
            as ValIO.Core.Plugins.IGeneratorRegistry;
        registry.Should().NotBeNull();
        registry!.FindPlugin("swift").Should().NotBeNull();
        registry.FindPlugin("SWIFT").Should().NotBeNull("case-insensitive");
    }
}
