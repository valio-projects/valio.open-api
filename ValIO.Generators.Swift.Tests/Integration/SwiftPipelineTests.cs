using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Swift.Options;
using ValIO.Generators.Swift.Tests.TestHelpers;

namespace ValIO.Generators.Swift.Tests.Integration;

[TestFixture]
public class SwiftPipelineTests
{
    private SwiftPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new SwiftPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task FullPipeline_GeneratesModels_Enums_AndClient()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var paths = ctx.GeneratedFiles.Select(f => f.RelativePath.Replace('\\', '/')).ToList();
        paths.Should().Contain(p => p.EndsWith("Models/User.swift", StringComparison.Ordinal));
        paths.Should().Contain(p => p.EndsWith("Models/UserRole.swift", StringComparison.Ordinal));
        paths.Should().Contain(p => p.Contains("/Api/", StringComparison.Ordinal) && p.EndsWith(".swift", StringComparison.Ordinal));
    }

    [Test]
    public void OptionsDescriptor_ParsesValidValues()
    {
        using var jsonDoc = JsonDocument.Parse(
            "{\"moduleName\":\"ApiKit\",\"anyMapping\":\"anycodable\",\"generateBuildFiles\":false}");
        var raw = new Dictionary<string, JsonElement>();
        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
            raw[prop.Name] = prop.Value;

        var descriptor = new SwiftOptionsDescriptor();
        var parsed = descriptor.ParseOptions(raw, out var errors);

        errors.Should().BeEmpty();
        var opts = parsed.Should().BeOfType<SwiftGenerationOptions>().Subject;
        opts.ModuleName.Should().Be("ApiKit");
        opts.AnyMapping.Should().Be("anycodable");
        opts.GenerateBuildFiles.Should().BeFalse();
    }

    [Test]
    public void OptionsDescriptor_FlagsUnknownAnyMapping()
    {
        using var jsonDoc = JsonDocument.Parse("{\"anyMapping\":\"mystery\"}");
        var raw = new Dictionary<string, JsonElement>
        {
            ["anyMapping"] = jsonDoc.RootElement.GetProperty("anyMapping")
        };

        var descriptor = new SwiftOptionsDescriptor();
        descriptor.ParseOptions(raw, out var errors);

        errors.Should().ContainSingle(e => e.Contains("Unknown anyMapping"));
    }

    [Test]
    public async Task PluginRegistry_FindsPlugin_ByAlias()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        await _harness.RunAsync(doc);

        var registry = (ValIO.Core.Plugins.IGeneratorRegistry)
            _harness.Services.GetService(typeof(ValIO.Core.Plugins.IGeneratorRegistry))!;

        registry.FindPlugin("swift").Should().NotBeNull();
        registry.FindPlugin("SWIFT").Should().NotBeNull();
        registry.FindPlugin("unknown").Should().BeNull();
    }

    [Test]
    public async Task GeneratedFiles_AreNonEmpty()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().NotBeEmpty();
        foreach (var f in ctx.GeneratedFiles)
            f.Content.Should().NotBeNullOrWhiteSpace($"file {f.RelativePath} must have content");
    }

    [Test]
    public async Task TypeMapper_RespectsScalarMappings()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("Models/User.swift", StringComparison.Ordinal));
        userFile.Content.Should().Contain(": Int");
        userFile.Content.Should().Contain(": String");
    }
}
