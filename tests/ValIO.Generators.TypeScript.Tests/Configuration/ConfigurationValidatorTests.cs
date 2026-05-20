using FluentAssertions;
using NUnit.Framework;
using ValIO.Configuration.Services;
using ValIO.Core.Models;
using ValIO.Core.Plugins;

namespace ValIO.Generators.TypeScript.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ConfigurationValidator"/>.
/// </summary>
[TestFixture]
public class ConfigurationValidatorTests
{
    private ConfigurationValidator _sut = null!;

    [SetUp]
    public void Setup() => _sut = new ConfigurationValidator();

    private static GenerationConfiguration Valid() => new()
    {
        LanguageId = "typescript",
        OutputPath = "./out",
        GenerateModels = true,
        GenerateClients = true,
        GenerateEnums = true,
        FileOrganization = new FileOrganization
        {
            SeparateModels = true,
            SeparateClients = true,
            ModelsPath = "models",
            ClientsPath = "api",
        }
    };

    [Test]
    public void Valid_NoErrorsOrWarnings()
    {
        var result = _sut.Validate(Valid());
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Test]
    public void MissingOutputPath_FailsValidation()
    {
        var config = Valid();
        config.OutputPath = null;
        var result = _sut.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("OutputPath"));
    }

    [Test]
    public void MissingLanguageId_FailsValidation()
    {
        var config = Valid();
        config.LanguageId = "";
        var result = _sut.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("LanguageId"));
    }

    [Test]
    public void NothingToGenerate_AddsWarning_StillValid()
    {
        var config = Valid();
        config.GenerateModels = false;
        config.GenerateClients = false;
        config.GenerateEnums = false;
        var result = _sut.Validate(config);
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("disabled"));
    }

    [Test]
    public void SeparateModelsTrue_ButNoModelsPath_AddsWarning()
    {
        var config = Valid();
        config.FileOrganization.ModelsPath = null;
        var result = _sut.Validate(config);
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("ModelsPath"));
    }

    [Test]
    public void SeparateClientsTrue_ButNoClientsPath_AddsWarning()
    {
        var config = Valid();
        config.FileOrganization.ClientsPath = null;
        var result = _sut.Validate(config);
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("ClientsPath"));
    }

    [Test]
    public void TwoErrors_AreBothReported()
    {
        var config = new GenerationConfiguration
        {
            LanguageId = "",
            OutputPath = "",
            FileOrganization = new FileOrganization(),
        };
        var result = _sut.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Test]
    public void WhitespaceOutputPath_TreatedAsMissing()
    {
        var config = Valid();
        config.OutputPath = "   ";
        var result = _sut.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("OutputPath"));
    }
}
