using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ValIO.Configuration.Models;
using ValIO.Configuration.Services;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ConfigurationLoader"/> — JSON/YAML parsing
/// and project → generation-config mapping (including option delegation
/// to the language plugin's <c>IGeneratorOptionsDescriptor</c>).
/// </summary>
[TestFixture]
public class ConfigurationLoaderTests
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
    public void LoadConfigFromJson_ParsesProjectsDictionary()
    {
        const string json = @"{
            ""projects"": {
                ""myProj"": {
                    ""language"": ""typescript"",
                    ""schema"": ""api.json"",
                    ""outputPath"": ""out"",
                    ""options"": { ""transports"": [""fetch""] }
                }
            }
        }";

        var config = _sut.LoadConfigFromJson(json);
        config.Projects.Should().ContainKey("myProj");
        var proj = config.Projects["myProj"];
        proj.Language.Should().Be("typescript");
        proj.Schema.Should().Be("api.json");
        proj.OutputPath.Should().Be("out");
    }

    [Test]
    public void LoadConfigFromJson_EmptyOrInvalidJson_Throws()
    {
        Action act = () => _sut.LoadConfigFromJson("not-json");
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void LoadConfigFromYaml_ParsesProjectsDictionary()
    {
        // Default YamlDotNet deserializer uses case-sensitive exact match,
        // so YAML keys mirror PascalCase property names.
        const string yaml = @"
Projects:
  myProj:
    Language: typescript
    Schema: api.yaml
    OutputPath: out
";
        var config = _sut.LoadConfigFromYaml(yaml);
        config.Projects.Should().ContainKey("myProj");
        config.Projects["myProj"].Schema.Should().Be("api.yaml");
    }

    [Test]
    public void MapToGenerationConfiguration_DelegatesOptionParsingToPlugin()
    {
        using var doc = JsonDocument.Parse(@"{ ""transports"": [""ky""], ""validation"": ""zod3"" }");
        var proj = new ProjectConfig
        {
            Language = "typescript",
            Schema = "x.json",
            OutputPath = "out",
            Options = new Dictionary<string, JsonElement>
            {
                ["transports"] = doc.RootElement.GetProperty("transports"),
                ["validation"] = doc.RootElement.GetProperty("validation"),
            },
        };

        var generation = _sut.MapToGenerationConfiguration(proj);

        generation.LanguageId.Should().Be("typescript");
        generation.PluginOptions.Should().BeOfType<TypeScriptGenerationOptions>();
        var opts = (TypeScriptGenerationOptions)generation.PluginOptions!;
        opts.Transports.Should().Contain("ky");
        opts.ValidationLibrary.Should().Be("zod3");
    }

    [Test]
    public void MapToGenerationConfiguration_UnknownLanguage_Throws()
    {
        var proj = new ProjectConfig { Language = "klingon", Schema = "x", OutputPath = "y" };
        Action act = () => _sut.MapToGenerationConfiguration(proj);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*klingon*");
    }

    [Test]
    public void MapToGenerationConfiguration_LanguageAlias_IsResolved()
    {
        var proj = new ProjectConfig { Language = "ts", Schema = "x", OutputPath = "y" };
        var generation = _sut.MapToGenerationConfiguration(proj);
        generation.LanguageId.Should().Be("ts", "the alias is preserved as the configured id");
    }

    [Test]
    public void MapToGenerationConfiguration_InvalidOption_Throws()
    {
        using var doc = JsonDocument.Parse(@"{ ""transports"": [""mystery""] }");
        var proj = new ProjectConfig
        {
            Language = "typescript", Schema = "x", OutputPath = "y",
            Options = new Dictionary<string, JsonElement>
            {
                ["transports"] = doc.RootElement.GetProperty("transports"),
            },
        };

        Action act = () => _sut.MapToGenerationConfiguration(proj);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown transport*");
    }

    [Test]
    public void MapToGenerationConfiguration_FileOrganization_IsMapped()
    {
        var proj = new ProjectConfig
        {
            Language = "typescript", Schema = "x", OutputPath = "y",
            FileOrganization = new ConfigFileOrganization
            {
                GroupingStrategy = "by-tag",
                SeparateModels = true,
                SeparateClients = false,
                ModelsPath = "dto",
                ClientsPath = "endpoints",
            },
        };

        var generation = _sut.MapToGenerationConfiguration(proj);
        generation.FileOrganization.GroupingStrategy.Should().Be(FileGroupingStrategy.ByTag);
        generation.FileOrganization.SeparateModels.Should().BeTrue();
        generation.FileOrganization.SeparateClients.Should().BeFalse();
        generation.FileOrganization.ModelsPath.Should().Be("dto");
        generation.FileOrganization.ClientsPath.Should().Be("endpoints");
    }

    [TestCase("pascalcase", NamingConvention.PascalCase)]
    [TestCase("pascal-case", NamingConvention.PascalCase)]
    [TestCase("camelcase", NamingConvention.CamelCase)]
    [TestCase("camel-case", NamingConvention.CamelCase)]
    [TestCase("snakecase", NamingConvention.SnakeCase)]
    [TestCase("kebab-case", NamingConvention.KebabCase)]
    public void MapToGenerationConfiguration_NamingConvention_HandlesAliases(string input, NamingConvention expected)
    {
        var proj = new ProjectConfig
        {
            Language = "typescript", Schema = "x", OutputPath = "y",
            NamingConvention = new ConfigNamingConventions
            {
                Models = input,
                Properties = input,
                Methods = input,
                Enums = input,
            },
        };
        var generation = _sut.MapToGenerationConfiguration(proj);
        generation.NamingConvention.Models.Should().Be(expected);
        generation.NamingConvention.Properties.Should().Be(expected);
    }

    [Test]
    public void MapToGenerationConfiguration_NamingAffixes_AreMappedAndNormalized()
    {
        var proj = new ProjectConfig
        {
            Language = "typescript", Schema = "x", OutputPath = "y",
            NamingConvention = new ConfigNamingConventions
            {
                ModelPrefix = "Api",
                ModelSuffix = "  Model  ",   // trimmed
                MethodSuffix = "Method",
                EnumPrefix = null,            // → empty
                EnumSuffix = "   ",           // whitespace → empty
            },
        };

        var nc = _sut.MapToGenerationConfiguration(proj).NamingConvention;
        nc.ModelPrefix.Should().Be("Api");
        nc.ModelSuffix.Should().Be("Model");
        nc.MethodPrefix.Should().BeEmpty();
        nc.MethodSuffix.Should().Be("Method");
        nc.EnumPrefix.Should().BeEmpty();
        nc.EnumSuffix.Should().BeEmpty();
    }

    [Test]
    public void MapToGenerationConfiguration_NoAffixes_DefaultToEmpty()
    {
        var proj = new ProjectConfig { Language = "typescript", Schema = "x", OutputPath = "y" };
        var nc = _sut.MapToGenerationConfiguration(proj).NamingConvention;
        nc.ModelPrefix.Should().BeEmpty();
        nc.ModelSuffix.Should().BeEmpty();
        nc.MethodPrefix.Should().BeEmpty();
        nc.MethodSuffix.Should().BeEmpty();
        nc.EnumPrefix.Should().BeEmpty();
        nc.EnumSuffix.Should().BeEmpty();
    }
}
