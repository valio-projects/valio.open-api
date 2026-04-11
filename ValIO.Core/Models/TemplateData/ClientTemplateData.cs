using ValIO.Core.Interfaces;
using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Core.Models.TemplateData;

/// <summary>
/// Универсальные данные для шаблона клиента (HTTP клиент)
/// </summary>
/// <typeparam name="TOptions">Тип опций (язык-специфичный)</typeparam>
/// <typeparam name="TOperationOptions">Тип опций операций (язык-специфичный)</typeparam>
public class ClientTemplateData<TOptions, TOperationOptions> : IClientTemplateData
    where TOptions : ClientTemplateDataOptions, new()
    where TOperationOptions : OperationTemplateDataOptions, new()
{
    /// <summary>
    /// Имя клиента
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Операции клиента
    /// </summary>
    public IReadOnlyList<OperationTemplateData<TOperationOptions>> Operations { get; set; } = Array.Empty<OperationTemplateData<TOperationOptions>>();
    
    /// <summary>
    /// Операции клиента для интерфейса IClientTemplateData
    /// </summary>
    IReadOnlyList<IOperationTemplateData> IClientTemplateData.Operations => Operations.Cast<IOperationTemplateData>().ToList();
    
    /// <summary>
    /// Язык генерации
    /// </summary>
    public GenerationLanguage Language { get; set; }
    
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
    public TOptions Options { get; set; } = new();
}

/// <summary>
/// Универсальные данные для шаблона клиента (HTTP клиент)
/// Безтипная версия для обратной совместимости
/// </summary>
public class ClientTemplateData : ClientTemplateData<ClientTemplateDataOptions, OperationTemplateDataOptions>
{
}
