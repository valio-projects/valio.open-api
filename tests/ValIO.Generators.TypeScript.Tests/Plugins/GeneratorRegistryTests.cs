using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript;

namespace ValIO.Generators.TypeScript.Tests.Plugins;

/// <summary>
/// Unit tests for <c>GeneratorRegistry</c> — language plugin lookup.
/// </summary>
[TestFixture]
public class GeneratorRegistryTests
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

    [TestCase("typescript")]
    [TestCase("ts")]
    public void FindsPlugin_ByCanonicalIdAndAlias(string id)
    {
        var plugin = _registry.FindPlugin(id);
        plugin.Should().NotBeNull();
        plugin!.LanguageId.Should().Be("typescript");
    }

    [TestCase("TYPESCRIPT")]
    [TestCase("TypeScript")]
    [TestCase("TS")]
    public void Lookup_IsCaseInsensitive(string id)
    {
        _registry.FindPlugin(id).Should().NotBeNull();
    }

    [Test]
    public void UnknownLanguage_ReturnsNull()
    {
        _registry.FindPlugin("php").Should().BeNull();
        _registry.FindPlugin("").Should().BeNull();
    }

    [Test]
    public void AllPlugins_ContainsRegisteredPlugin()
    {
        _registry.AllPlugins.Should().ContainSingle(p => p.LanguageId == "typescript");
    }
}
