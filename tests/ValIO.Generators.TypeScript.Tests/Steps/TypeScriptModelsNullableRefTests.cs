using FluentAssertions;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Интерфейс-модели для паттерна <c>{allOf:[{$ref}],nullable:true}</c>: свойство должно
/// рендериться как <c>T | null | undefined</c> (раньше молча терялся <c>| null</c>),
/// а настоящее 2-членное наследование — как <c>extends Base</c>.
/// </summary>
[TestFixture]
public class TypeScriptModelsNullableRefTests
{
    private PipelineTestHarness _harness = null!;
    private OpenApiDocument _document = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _harness = new PipelineTestHarness();
        _document = await PipelineTestHarness.LoadDocumentAsync("nullable-ref-allof.json");
    }

    [OneTimeTearDown]
    public void OneTimeTeardown() => _harness.Dispose();

    private async Task<string> ModelAsync(string camelModel)
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        return ctx.GeneratedFiles
            .First(f => f.RelativePath.Replace('\\', '/')
                .EndsWith($"models/{camelModel}/{camelModel}.ts", StringComparison.Ordinal))
            .Content;
    }

    [Test]
    public async Task NullableRefProperty_RendersNullableAndOptional()
    {
        var content = await ModelAsync("wrapper");
        // Optional (?) and nullable (| null) — matches the C# `CompatibilityMode?` intent.
        content.Should().MatchRegex(@"compat\?\s*:\s*CompatibilityMode\s*\|\s*null");
    }

    [Test]
    public async Task MultiElementAllOf_RendersExtendsBase()
    {
        var content = await ModelAsync("inherited");
        content.Should().MatchRegex(@"interface\s+Inherited\s+extends\s+Base");
        content.Should().Contain("extra");
    }
}
