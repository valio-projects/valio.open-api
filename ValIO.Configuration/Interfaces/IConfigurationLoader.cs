using ValIO.Core.Models;

namespace ValIO.Configuration.Interfaces;

/// <summary>
/// Интерфейс для загрузки конфигурации
/// </summary>
public interface IConfigurationLoader
{
    Task<GenerationConfiguration> LoadFromFileAsync(string filePath);
    GenerationConfiguration LoadFromJson(string json);
    GenerationConfiguration LoadFromYaml(string yaml);
}

