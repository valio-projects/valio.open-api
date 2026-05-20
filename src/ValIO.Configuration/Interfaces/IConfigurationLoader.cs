using ValIO.Configuration.Models;
using ValIO.Core.Models;

namespace ValIO.Configuration.Interfaces;

/// <summary>
/// Интерфейс для загрузки конфигурации
/// </summary>
public interface IConfigurationLoader
{
    /// <summary>
    /// Загружает конфигурацию из файла
    /// </summary>
    Task<ValIOConfig> LoadConfigFromFileAsync(string filePath);
    
    /// <summary>
    /// Загружает конфигурацию из JSON
    /// </summary>
    ValIOConfig LoadConfigFromJson(string json);
    
    /// <summary>
    /// Загружает конфигурацию из YAML
    /// </summary>
    ValIOConfig LoadConfigFromYaml(string yaml);
    
    /// <summary>
    /// Преобразует конфигурацию проекта в GenerationConfiguration
    /// </summary>
    GenerationConfiguration MapToGenerationConfiguration(ProjectConfig projectConfig);
}

