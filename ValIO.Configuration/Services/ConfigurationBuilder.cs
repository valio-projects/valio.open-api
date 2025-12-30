using ValIO.Core.Models;

namespace ValIO.Configuration.Services;

/// <summary>
/// Строитель конфигурации (Builder Pattern)
/// </summary>
public class ConfigurationBuilder
{
    private GenerationLanguage _language;
    private GenerationOptions? _options;
    private FileOrganization _fileOrganization = new();
    private NamingConventions _namingConvention = new();
    private string _outputPath = string.Empty;
    private string? _namespace;
    private bool _generateEnums = true;
    private bool _generateModels = true;
    private bool _generateClients = true;

    public ConfigurationBuilder WithLanguage(GenerationLanguage language)
    {
        _language = language;
        return this;
    }

    public ConfigurationBuilder WithOptions(GenerationOptions options)
    {
        _options = options;
        return this;
    }

    public ConfigurationBuilder WithFileOrganization(FileOrganization fileOrganization)
    {
        _fileOrganization = fileOrganization;
        return this;
    }

    public ConfigurationBuilder WithNamingConvention(NamingConventions namingConvention)
    {
        _namingConvention = namingConvention;
        return this;
    }

    public ConfigurationBuilder WithOutputPath(string outputPath)
    {
        _outputPath = outputPath;
        return this;
    }

    public ConfigurationBuilder WithNamespace(string? @namespace)
    {
        _namespace = @namespace;
        return this;
    }

    public ConfigurationBuilder WithGenerateEnums(bool generateEnums)
    {
        _generateEnums = generateEnums;
        return this;
    }

    public ConfigurationBuilder WithGenerateModels(bool generateModels)
    {
        _generateModels = generateModels;
        return this;
    }

    public ConfigurationBuilder WithGenerateClients(bool generateClients)
    {
        _generateClients = generateClients;
        return this;
    }

    public GenerationConfiguration Build()
    {
        return new GenerationConfiguration
        {
            Language = _language,
            Options = _options,
            FileOrganization = _fileOrganization,
            NamingConvention = _namingConvention,
            OutputPath = _outputPath,
            Namespace = _namespace,
            GenerateEnums = _generateEnums,
            GenerateModels = _generateModels,
            GenerateClients = _generateClients
        };
    }
}

