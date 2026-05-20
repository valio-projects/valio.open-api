using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ValIO.Configuration.Models;
using ValIO.Configuration.Services;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript;

namespace ValIO.Generators.TypeScript.Tests.Configuration;

[TestFixture]
public class ConfigurationLoaderEdgeCasesTests
{
    private ConfigurationLoader _sut = null!;

    [SetUp]
    public void Setup()
    {
        var registry = new GeneratorRegistry(new IGeneratorPlugin[]
        {
            new TypeScriptGeneratorPlugin(),
        });
        _sut = new ConfigurationLoader(NullLogger<ConfigurationLoader>.Instance, registry);
    }

    [Test]
    public void EmptyProjectsObject_ParsesToZeroProjects()
    {
        var config = _sut.LoadConfigFromJson(@"{ ""projects"": {} }");
        config.Projects.Should().BeEmpty();
    }

    [Test]
    public void MissingProjectsKey_ParsesToZeroProjects()
    {
        var config = _sut.LoadConfigFromJson("{}");
        config.Projects.Should().BeEmpty();
    }

    [Test]
    public void WhitespaceJson_Throws()
    {
        Action act = () => _sut.LoadConfigFromJson("   ");
        act.Should().Throw<Exception>();
    }

    [Test]
    public void OptionsAsNonObject_HandlesGracefully()
    {
        // `options` is null/missing — should not throw on map.
        var proj = new ProjectConfig
        {
            Language = "typescript",
            Schema = "x",
            OutputPath = "y",
            Options = null,
        };
        Action act = () => _sut.MapToGenerationConfiguration(proj);
        act.Should().NotThrow();
    }

    [Test]
    public void DefaultLanguage_FallsBackToTypeScript()
    {
        // Empty language string → defaults to "typescript" per loader logic.
        var proj = new ProjectConfig
        {
            Language = "",
            Schema = "x",
            OutputPath = "y",
        };
        var config = _sut.MapToGenerationConfiguration(proj);
        config.LanguageId.Should().Be("typescript");
    }
}
