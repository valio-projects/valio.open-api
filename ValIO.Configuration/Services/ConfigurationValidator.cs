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

        // Проверка обязательных полей
        if (string.IsNullOrWhiteSpace(config.OutputPath))
            errors.Add("OutputPath is required");

        // Валидация языка
        if (!Enum.IsDefined(typeof(GenerationLanguage), config.Language))
            errors.Add($"Invalid language: {config.Language}");

        // Валидация опций для TypeScript
        if (config.Language == GenerationLanguage.TypeScript && config.Options != null)
        {
            if (config.Options.ValidationLibrary.HasValue && 
                !Enum.IsDefined(typeof(ValidationLibrary), config.Options.ValidationLibrary.Value))
                errors.Add($"Invalid validation library: {config.Options.ValidationLibrary}");

            if (config.Options.QueryWrapperLibrary.HasValue &&
                !Enum.IsDefined(typeof(QueryWrapperLibrary), config.Options.QueryWrapperLibrary.Value))
                errors.Add($"Invalid query wrapper library: {config.Options.QueryWrapperLibrary}");
        }

        // Предупреждения
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

