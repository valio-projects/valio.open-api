using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Services;

/// <summary>
/// Сборщик зависимостей из TypeInfo и TemplateData
/// </summary>
public class DependencyCollector
{
    private readonly IDependencyAnalyzer _dependencyAnalyzer;

    public DependencyCollector(IDependencyAnalyzer dependencyAnalyzer)
    {
        _dependencyAnalyzer = dependencyAnalyzer ?? throw new ArgumentNullException(nameof(dependencyAnalyzer));
    }

    /// <summary>
    /// Собирает зависимости из TypeInfo
    /// </summary>
    public IReadOnlyList<string> CollectFromTypeInfo(TypeInfo typeInfo)
    {
        var dependencies = new HashSet<string>(16 * 1024);
        _dependencyAnalyzer.CollectDependenciesFromTypeInfo(typeInfo, dependencies);
        return dependencies.ToList();
    }

    /// <summary>
    /// Собирает зависимости из модели
    /// </summary>
    public IReadOnlyList<string> CollectFromModel(ModelTemplateData model)
    {
        return _dependencyAnalyzer.CollectAllDependencies(model);
    }

    /// <summary>
    /// Собирает зависимости из операции
    /// </summary>
    public IReadOnlyList<string> CollectFromOperation(OperationTemplateData operation)
    {
        return _dependencyAnalyzer.CollectAllDependencies(operation);
    }

    /// <summary>
    /// Собирает зависимости из клиента
    /// </summary>
    public IReadOnlyList<string> CollectFromClient(ClientTemplateData client)
    {
        return _dependencyAnalyzer.CollectAllDependencies(client);
    }

    /// <summary>
    /// Определяет пути импорта для зависимостей
    /// </summary>
    public Dictionary<string, string> ResolveImportPaths(
        IReadOnlyList<string> dependencies,
        string? modelsPath = null,
        string? schemasPath = null)
    {
        return _dependencyAnalyzer.ResolveImportPaths(dependencies, modelsPath, schemasPath);
    }

    /// <summary>
    /// Собирает и разрешает пути импорта для модели
    /// </summary>
    public Dictionary<string, string> CollectAndResolveImportPaths(
        ModelTemplateData model,
        string? modelsPath = null,
        string? schemasPath = null)
    {
        var dependencies = CollectFromModel(model);
        return ResolveImportPaths(dependencies, modelsPath, schemasPath);
    }

    /// <summary>
    /// Собирает и разрешает пути импорта для операции
    /// </summary>
    public Dictionary<string, string> CollectAndResolveImportPaths(
        OperationTemplateData operation,
        string? modelsPath = null,
        string? schemasPath = null)
    {
        var dependencies = CollectFromOperation(operation);
        return ResolveImportPaths(dependencies, modelsPath, schemasPath);
    }

    /// <summary>
    /// Собирает и разрешает пути импорта для клиента
    /// </summary>
    public Dictionary<string, string> CollectAndResolveImportPaths(
        ClientTemplateData client,
        string? modelsPath = null,
        string? schemasPath = null)
    {
        var dependencies = CollectFromClient(client);
        return ResolveImportPaths(dependencies, modelsPath, schemasPath);
    }
}
