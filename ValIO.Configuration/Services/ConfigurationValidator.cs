using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Models;
using ValIO.Core.Models;

namespace ValIO.Configuration.Services;

/// <summary>
/// Валидатор конфигурации
/// </summary>
public class ConfigurationValidator : IConfigurationValidator
{
    public ValidationResult Validate(GenerationConfiguration config)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(config.OutputPath))
            errors.Add("OutputPath is required");

        if (string.IsNullOrWhiteSpace(config.LanguageId))
            errors.Add("LanguageId is required");

        if (!config.GenerateModels && !config.GenerateClients && !config.GenerateEnums)
            warnings.Add("All generation options are disabled. No code will be generated.");

        if (config.FileOrganization.SeparateModels && string.IsNullOrWhiteSpace(config.FileOrganization.ModelsPath))
            warnings.Add("SeparateModels is true but ModelsPath is not specified. Default path will be used.");

        if (config.FileOrganization.SeparateClients && string.IsNullOrWhiteSpace(config.FileOrganization.ClientsPath))
            warnings.Add("SeparateClients is true but ClientsPath is not specified. Default path will be used.");

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
}
