using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Python.Options;
using ValIO.Generators.Python.Tests.TestHelpers;

namespace ValIO.Generators.Python.Tests.Integration;

[TestFixture]
public class PythonPipelineTests
{
    private PythonPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PythonPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task FullPipeline_GeneratesModels_Enums_AndClient()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var paths = ctx.GeneratedFiles.Select(f => f.RelativePath.Replace('\\', '/')).ToList();
        paths.Should().Contain("models/user.py");
        paths.Should().Contain("models/user_role.py");
        paths.Should().Contain(p => p.StartsWith("api/", StringComparison.Ordinal) && p.EndsWith(".py", StringComparison.Ordinal));
    }

    [Test]
    public void OptionsDescriptor_ParsesValidValues()
    {
        using var jsonDoc = JsonDocument.Parse(
            "{\"pythonVersion\":\"3.12\",\"httpxMode\":\"sync\",\"validator\":\"pydantic\",\"rootModule\":\"my_api\"}");
        var raw = new Dictionary<string, JsonElement>();
        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
            raw[prop.Name] = prop.Value;

        var descriptor = new PythonOptionsDescriptor();
        var parsed = descriptor.ParseOptions(raw, out var errors);

        errors.Should().BeEmpty();
        var opts = parsed.Should().BeOfType<PythonGenerationOptions>().Subject;
        opts.PythonVersion.Should().Be("3.12");
        opts.HttpxMode.Should().Be("sync");
        opts.Validator.Should().Be("pydantic");
        opts.RootModule.Should().Be("my_api");
    }

    [Test]
    public void OptionsDescriptor_FlagsUnknownHttpxMode()
    {
        using var jsonDoc = JsonDocument.Parse("{\"httpxMode\":\"mystery\"}");
        var raw = new Dictionary<string, JsonElement>
        {
            ["httpxMode"] = jsonDoc.RootElement.GetProperty("httpxMode")
        };

        var descriptor = new PythonOptionsDescriptor();
        descriptor.ParseOptions(raw, out var errors);

        errors.Should().ContainSingle(e => e.Contains("Unknown httpxMode"));
    }

    [Test]
    public async Task PluginRegistry_FindsPlugin_ByAlias()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        await _harness.RunAsync(doc);

        var registry = (ValIO.Core.Plugins.IGeneratorRegistry)
            _harness.Services.GetService(typeof(ValIO.Core.Plugins.IGeneratorRegistry))!;

        registry.FindPlugin("python").Should().NotBeNull();
        registry.FindPlugin("py").Should().NotBeNull();
        registry.FindPlugin("PYTHON").Should().NotBeNull();
        registry.FindPlugin("unknown").Should().BeNull();
    }

    [Test]
    public async Task GeneratedFiles_AreNonEmpty()
    {
        var doc = await PythonPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        ctx.GeneratedFiles.Should().NotBeEmpty();
        foreach (var f in ctx.GeneratedFiles)
        {
            f.Content.Should().NotBeNullOrWhiteSpace($"file {f.RelativePath} must have content");
        }
    }
}
