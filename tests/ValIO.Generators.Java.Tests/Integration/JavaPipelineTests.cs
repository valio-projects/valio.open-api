using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Java.Options;
using ValIO.Generators.Java.Tests.TestHelpers;

namespace ValIO.Generators.Java.Tests.Integration;

[TestFixture]
public class JavaPipelineTests
{
    private JavaPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new JavaPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task FullPipeline_GeneratesModels_Enums_AndClient()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var paths = ctx.GeneratedFiles.Select(f => f.RelativePath.Replace('\\', '/')).ToList();
        paths.Should().Contain("models/User.java");
        paths.Should().Contain("models/UserRole.java");
        paths.Should().Contain(p => p.StartsWith("api/", StringComparison.Ordinal) && p.EndsWith(".java", StringComparison.Ordinal));
    }

    [Test]
    public void OptionsDescriptor_ParsesValidValues()
    {
        using var jsonDoc = JsonDocument.Parse(
            "{\"package\":\"io.acme.api\",\"useHttpClient\":\"okhttp\",\"generateBuildFiles\":true}");
        var raw = new Dictionary<string, JsonElement>();
        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
            raw[prop.Name] = prop.Value;

        var descriptor = new JavaOptionsDescriptor();
        var parsed = descriptor.ParseOptions(raw, out var errors);

        errors.Should().BeEmpty();
        var opts = parsed.Should().BeOfType<JavaGenerationOptions>().Subject;
        opts.Package.Should().Be("io.acme.api");
        opts.UseHttpClient.Should().Be(JavaOptionIds.HttpClient.OkHttp);
        opts.GenerateBuildFiles.Should().BeTrue();
    }

    [Test]
    public void OptionsDescriptor_FlagsUnknownHttpClient()
    {
        using var jsonDoc = JsonDocument.Parse("{\"useHttpClient\":\"mystery\"}");
        var raw = new Dictionary<string, JsonElement>
        {
            ["useHttpClient"] = jsonDoc.RootElement.GetProperty("useHttpClient")
        };

        var descriptor = new JavaOptionsDescriptor();
        descriptor.ParseOptions(raw, out var errors);

        errors.Should().ContainSingle(e => e.Contains("Unknown Java HTTP client"));
    }

    [Test]
    public async Task PluginRegistry_FindsPlugin_ByAlias()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        await _harness.RunAsync(doc);

        var registry = (ValIO.Core.Plugins.IGeneratorRegistry)
            _harness.Services.GetService(typeof(ValIO.Core.Plugins.IGeneratorRegistry))!;

        registry.FindPlugin("java").Should().NotBeNull();
        registry.FindPlugin("java21").Should().NotBeNull();
        registry.FindPlugin("JAVA").Should().NotBeNull();
        registry.FindPlugin("unknown").Should().BeNull();
    }

    [Test]
    public async Task GeneratedFiles_AreNonEmpty()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().NotBeEmpty();
        foreach (var f in ctx.GeneratedFiles)
        {
            f.Content.Should().NotBeNullOrWhiteSpace($"file {f.RelativePath} must have content");
        }
    }

    [Test]
    public async Task GenerateBuildFiles_EmitsGradleArtifacts()
    {
        var doc = await JavaPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new JavaGenerationOptions { GenerateBuildFiles = true });

        var paths = ctx.GeneratedFiles.Select(f => f.RelativePath.Replace('\\', '/')).ToList();
        paths.Should().Contain("build.gradle.kts");
        paths.Should().Contain("gradle.properties");
    }
}
