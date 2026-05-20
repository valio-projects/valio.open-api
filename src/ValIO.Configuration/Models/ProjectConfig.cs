namespace ValIO.Configuration.Models;

/// <summary>
/// Конфигурация одного проекта/сервиса
/// </summary>
public class ProjectConfig
{
    /// <summary>
    /// Язык программирования для генерации
    /// </summary>
    public string Language { get; set; } = string.Empty;
    
    /// <summary>
    /// Путь или URL к OpenAPI схеме
    /// </summary>
    public string Schema { get; set; } = string.Empty;
    
    /// <summary>
    /// Путь для сохранения сгенерированных файлов
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Генерировать модели (включая enums)
    /// </summary>
    public bool GenerateModels { get; set; } = true;
    
    /// <summary>
    /// Генерировать HTTP клиенты
    /// </summary>
    public bool GenerateClients { get; set; } = true;
    
    /// <summary>
    /// Опции генерации (language-specific)
    /// </summary>
    public Dictionary<string, System.Text.Json.JsonElement>? Options { get; set; }
    
    /// <summary>
    /// Организация файлов
    /// </summary>
    public ConfigFileOrganization? FileOrganization { get; set; }
    
    /// <summary>
    /// Конвенции именования
    /// </summary>
    public ConfigNamingConventions? NamingConvention { get; set; }
    
    /// <summary>
    /// Namespace (для C#) или не используется (для TypeScript)
    /// </summary>
    public string? Namespace { get; set; }
}

/// <summary>
/// Корневая конфигурация с проектами
/// </summary>
public class ValIOConfig
{
    /// <summary>
    /// Словарь проектов: ключ - название проекта/сервиса, значение - конфигурация проекта
    /// </summary>
    public Dictionary<string, ProjectConfig> Projects { get; set; } = new();
}
