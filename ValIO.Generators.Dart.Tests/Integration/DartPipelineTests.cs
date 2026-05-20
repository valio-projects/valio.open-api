using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Dart.Tests.TestHelpers;

namespace ValIO.Generators.Dart.Tests.Integration;

[TestFixture]
public class DartPipelineTests
{
    private DartPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new DartPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task FullPipeline_EmitsModelsEnumsAndClient()
    {
        var doc = await DartPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var paths = ctx.GeneratedFiles.Select(f => f.RelativePath.Replace('\\', '/')).ToList();
        paths.Should().Contain("models/user.dart");
        paths.Should().Contain("models/user_role.dart");
        paths.Should().Contain(p => p.StartsWith("api/") && p.EndsWith(".dart"));
    }

    [Test]
    public async Task EveryFile_HasNonEmptyContent()
    {
        var doc = await DartPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles)
            f.Content.Should().NotBeNullOrWhiteSpace(f.RelativePath);
    }

    [Test]
    public async Task UserClass_DeclaresClassKeyword()
    {
        var doc = await DartPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var user = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("user.dart", StringComparison.OrdinalIgnoreCase));
        user.Content.Should().Contain("class User");
    }

    [Test]
    public async Task UserRoleEnum_DeclaresEnumKeyword()
    {
        var doc = await DartPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var role = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("user_role.dart", StringComparison.OrdinalIgnoreCase));
        role.Content.Should().Contain("enum UserRole");
    }

    [Test]
    public async Task EveryDartFile_BalancedBraces()
    {
        var doc = await DartPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        foreach (var f in ctx.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".dart")))
        {
            // Strip string literals (single and double quoted) before counting braces.
            var stripped = System.Text.RegularExpressions.Regex.Replace(f.Content,
                @"""([^""\\]|\\.)*""", "\"\"");
            stripped = System.Text.RegularExpressions.Regex.Replace(stripped,
                @"'([^'\\]|\\.)*'", "''");
            var open = stripped.Count(c => c == '{');
            var close = stripped.Count(c => c == '}');
            open.Should().Be(close, $"{f.RelativePath} has unbalanced braces");
        }
    }

    [Test]
    public async Task ServiceFile_ImportsDio()
    {
        var doc = await DartPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var service = ctx.GeneratedFiles.First(f =>
            f.RelativePath.StartsWith("api/", StringComparison.OrdinalIgnoreCase));
        service.Content.Should().Contain("package:dio/dio.dart");
    }

    [Test]
    public async Task PluginRegistry_FindsPluginByAlias()
    {
        var doc = await DartPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        await _harness.RunAsync(doc);

        var registry = (ValIO.Core.Plugins.IGeneratorRegistry)
            _harness.Services.GetService(typeof(ValIO.Core.Plugins.IGeneratorRegistry))!;
        registry.FindPlugin("dart").Should().NotBeNull();
        registry.FindPlugin("dart-dio").Should().NotBeNull();
        registry.FindPlugin("nonsense").Should().BeNull();
    }
}
