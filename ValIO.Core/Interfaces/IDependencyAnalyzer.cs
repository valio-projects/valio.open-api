using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для анализа зависимостей между типами
/// </summary>
public interface IDependencyAnalyzer
{
    /// <summary>
    /// Анализирует зависимости модели
    /// </summary>
    IReadOnlyList<string> AnalyzeModelDependencies(ModelTemplateData model);

    /// <summary>
    /// Анализирует зависимости операции
    /// </summary>
    IReadOnlyList<string> AnalyzeOperationDependencies(OperationTemplateData operation);

    /// <summary>
    /// Собирает все зависимости из TypeInfo
    /// </summary>
    void CollectDependenciesFromTypeInfo(TypeInfo typeInfo, HashSet<string> dependencies);

    /// <summary>
    /// Собирает все зависимости из модели
    /// </summary>
    IReadOnlyList<string> CollectAllDependencies(ModelTemplateData model);

    /// <summary>
    /// Собирает все зависимости из операции
    /// </summary>
    IReadOnlyList<string> CollectAllDependencies(OperationTemplateData operation);

    /// <summary>
    /// Собирает все зависимости из клиента
    /// </summary>
    IReadOnlyList<string> CollectAllDependencies(ClientTemplateData client);

    /// <summary>
    /// Определяет пути импорта для зависимостей
    /// </summary>
    Dictionary<string, string> ResolveImportPaths(
        IReadOnlyList<string> dependencies,
        string? modelsPath = null,
        string? schemasPath = null);
}
