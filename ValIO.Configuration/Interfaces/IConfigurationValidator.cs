using ValIO.Configuration.Models;
using ValIO.Core.Models;

namespace ValIO.Configuration.Interfaces;

/// <summary>
/// Интерфейс для валидации конфигурации
/// </summary>
public interface IConfigurationValidator
{
    ValidationResult Validate(GenerationConfiguration config);
}

