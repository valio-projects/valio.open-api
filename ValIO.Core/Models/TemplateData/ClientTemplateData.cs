namespace ValIO.Core.Models.TemplateData;

/// <summary>
/// Универсальные данные для шаблона клиента (HTTP клиент)
/// </summary>
public class ClientTemplateData
{
    /// <summary>
    /// Имя клиента
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Операции клиента
    /// </summary>
    public IReadOnlyList<OperationTemplateData> Operations { get; set; } = Array.Empty<OperationTemplateData>();
    
    /// <summary>
    /// Язык генерации
    /// </summary>
    public GenerationLanguage Language { get; set; }
    
    /// <summary>
    /// HTTP клиент библиотека (Fetch, Axios, HttpClient и т.д.)
    /// </summary>
    public HttpClientLibrary HttpClientLibrary { get; set; } = HttpClientLibrary.Fetch;
    
    /// <summary>
    /// Список зависимостей (имена типов, которые нужно импортировать)
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Namespace (для C#)
    /// </summary>
    public string? Namespace { get; set; }
    
    /// <summary>
    /// Путь к папке с моделями для импортов (для TypeScript)
    /// </summary>
    public string? ModelsPath { get; set; }
    
    /// <summary>
    /// Дополнительные опции для генерации (язык-специфичные)
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();
}
