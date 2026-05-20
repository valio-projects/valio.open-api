using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Kotlin.Options;
using ValIO.Generators.Kotlin.Tests.TestHelpers;

namespace ValIO.Generators.Kotlin.Tests.Integration;

[TestFixture]
public class KotlinPipelineTests
{
    private KotlinPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new KotlinPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task FullPipeline_GeneratesModels_Enums_AndClient()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var paths = ctx.GeneratedFiles.Select(f => f.RelativePath.Replace('\\', '/')).ToList();
        paths.Should().Contain(p => p.EndsWith("models/User.kt", StringComparison.Ordinal));
        paths.Should().Contain(p => p.EndsWith("models/UserRole.kt", StringComparison.Ordinal));
        paths.Should().Contain(p => p.Contains("/api/", StringComparison.Ordinal) && p.EndsWith(".kt", StringComparison.Ordinal));
    }

    [Test]
    public void OptionsDescriptor_ParsesValidValues()
    {
        using var jsonDoc = JsonDocument.Parse(
            "{\"package\":\"com.acme.gen\",\"serialization\":\"kotlinx\",\"generateBuildFiles\":false}");
        var raw = new Dictionary<string, JsonElement>();
        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
            raw[prop.Name] = prop.Value;

        var descriptor = new KotlinOptionsDescriptor();
        var parsed = descriptor.ParseOptions(raw, out var errors);

        errors.Should().BeEmpty();
        var opts = parsed.Should().BeOfType<KotlinGenerationOptions>().Subject;
        opts.Package.Should().Be("com.acme.gen");
        opts.Serialization.Should().Be("kotlinx");
        opts.GenerateBuildFiles.Should().BeFalse();
    }

    [Test]
    public void OptionsDescriptor_FlagsUnknownSerialization()
    {
        using var jsonDoc = JsonDocument.Parse("{\"serialization\":\"mystery\"}");
        var raw = new Dictionary<string, JsonElement>
        {
            ["serialization"] = jsonDoc.RootElement.GetProperty("serialization")
        };

        var descriptor = new KotlinOptionsDescriptor();
        descriptor.ParseOptions(raw, out var errors);

        errors.Should().ContainSingle(e => e.Contains("Unknown serialization"));
    }

    [Test]
    public async Task PluginRegistry_FindsPlugin_ByAlias()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        await _harness.RunAsync(doc);

        var registry = (ValIO.Core.Plugins.IGeneratorRegistry)
            _harness.Services.GetService(typeof(ValIO.Core.Plugins.IGeneratorRegistry))!;

        registry.FindPlugin("kotlin").Should().NotBeNull();
        registry.FindPlugin("kt").Should().NotBeNull();
        registry.FindPlugin("KOTLIN").Should().NotBeNull();
        registry.FindPlugin("unknown").Should().BeNull();
    }

    [Test]
    public async Task GeneratedFiles_AreNonEmpty()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().NotBeEmpty();
        foreach (var f in ctx.GeneratedFiles)
            f.Content.Should().NotBeNullOrWhiteSpace($"file {f.RelativePath} must have content");
    }

    [Test]
    public async Task TypeMapper_RespectsScalarMappings()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var userFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/User.kt", StringComparison.Ordinal));
        userFile.Content.Should().Contain(": Int");
        userFile.Content.Should().Contain(": String");
    }
}
