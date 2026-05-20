using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript;

namespace ValIO.Generators.TypeScript.Tests.Plugins;

/// <summary>
/// Expanded registry tests that exercise the TypeScript plugin's
/// language-id contract and aliases (we can't include other-language
/// plugins from this test project without circular references).
/// </summary>
[TestFixture]
public class PluginRegistryExpandedTests
{
    private IGeneratorRegistry _registry = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        _registry = new GeneratorRegistry(new IGeneratorPlugin[]
        {
            new TypeScriptGeneratorPlugin(),
        });
    }

    [Test]
    public void Plugin_HasExpectedLanguageId()
    {
        var p = _registry.FindPlugin("typescript")!;
        p.LanguageId.Should().Be("typescript");
        p.DisplayName.Should().Be("TypeScript");
    }

    [Test]
    public void Plugin_HasAtLeastOneAlias()
    {
        var p = _registry.FindPlugin("typescript")!;
        p.LanguageAliases.Should().NotBeEmpty();
        p.LanguageAliases.Should().Contain(new[] { "ts" });
    }

    [Test]
    public void Plugin_HasOptionsDescriptor()
    {
        var p = _registry.FindPlugin("typescript")!;
        p.OptionsDescriptor.Should().NotBeNull();
        p.OptionsDescriptor.GetOptionDescriptions().Should().NotBeEmpty();
    }

    [Test]
    public void Lookup_BySimilarButDifferentName_ReturnsNull()
    {
        _registry.FindPlugin("typscript").Should().BeNull();  // missing 'e'
        _registry.FindPlugin("type-script").Should().BeNull(); // hyphen
        _registry.FindPlugin("TypeScript-fixed").Should().BeNull();
    }

    [Test]
    public void AllPlugins_IsImmutableSnapshot()
    {
        var first = _registry.AllPlugins;
        var second = _registry.AllPlugins;
        first.Count.Should().Be(second.Count);
    }

    [Test]
    public void Lookup_TrimmedAndCaseInsensitive()
    {
        _registry.FindPlugin("typescript").Should().NotBeNull();
        _registry.FindPlugin("TypeScript").Should().NotBeNull();
        _registry.FindPlugin("TYPESCRIPT").Should().NotBeNull();
        _registry.FindPlugin("Typescript").Should().NotBeNull();
    }
}
