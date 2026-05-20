using System.Collections.Immutable;
using ValIO.Core.IR.Types;

namespace ValIO.Core.IR.Builders;

/// <summary>
/// Строит граф зависимостей IR‑типов и топологический порядок генерации.
/// Раньше эта логика жила внутри <see cref="IrBuilder"/>; вынесение в
/// отдельный класс упрощает unit‑тестирование (сортировка проверяется
/// без OpenAPI документа) и облегчает само‑документирование.
/// </summary>
public sealed class IrTypeDependencyGraphBuilder
{
    /// <summary>
    /// Строит граф зависимостей по моделям и enum‑ам.
    /// Клиенты не вносят новых вершин — их зависимости уже учтены
    /// внутри тел моделей.
    /// </summary>
    public IrTypeDependencyGraph Build(
        IReadOnlyList<IrModel> models,
        IReadOnlyList<IrEnum> enums)
    {
        var dependencies = new Dictionary<string, HashSet<string>>();
        var allTypeNames = new HashSet<string>();

        foreach (var model in models)
        {
            allTypeNames.Add(model.Name);
            dependencies[model.Name] = new HashSet<string>();
        }

        foreach (var enumModel in enums)
        {
            allTypeNames.Add(enumModel.Name);
            dependencies[enumModel.Name] = new HashSet<string>();
        }

        foreach (var model in models)
        {
            dependencies[model.Name] = ExtractDependenciesFromModel(model);
        }

        var topologicalOrder = TopologicalSort(dependencies, allTypeNames);

        var immutableDependencies = dependencies.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value.ToList());

        return new IrTypeDependencyGraph
        {
            Dependencies = immutableDependencies.ToImmutableDictionary(),
            TopologicalOrder = topologicalOrder,
        };
    }

    /// <summary>
    /// Зависимости модели — модели/enum'ы, на которые ссылаются её свойства
    /// и типы её композиции (allOf/oneOf/anyOf).
    /// </summary>
    public HashSet<string> ExtractDependenciesFromModel(IrModel model)
    {
        var dependencies = new HashSet<string>();

        foreach (var property in model.Properties)
            dependencies.UnionWith(ExtractDependenciesFromType(property.Type));

        if (model.Composition != null)
        {
            foreach (var type in model.Composition.Types)
                dependencies.UnionWith(ExtractDependenciesFromType(type));
        }

        return dependencies;
    }

    /// <summary>
    /// Рекурсивно извлекает имена модельных ссылок из произвольного IR‑типа.
    /// Учитывает array/dictionary/union/intersection/nullable/optional.
    /// </summary>
    public HashSet<string> ExtractDependenciesFromType(IrType type)
    {
        var dependencies = new HashSet<string>();
        Walk(type, dependencies);
        return dependencies;
    }

    private static void Walk(IrType type, HashSet<string> sink)
    {
        switch (type)
        {
            case IrModelReference modelRef:
                sink.Add(modelRef.ModelName);
                break;
            case IrArrayType arr:
                Walk(arr.ElementType, sink);
                break;
            case IrDictionaryType dict:
                Walk(dict.KeyType, sink);
                Walk(dict.ValueType, sink);
                break;
            case IrUnionType u:
                foreach (var t in u.Types) Walk(t, sink);
                break;
            case IrIntersectionType i:
                foreach (var t in i.Types) Walk(t, sink);
                break;
            case IrNullableType n:
                Walk(n.InnerType, sink);
                break;
            case IrOptionalType o:
                Walk(o.InnerType, sink);
                break;
        }
    }

    /// <summary>
    /// Топологическая сортировка с защитой от циклов: при обнаружении
    /// цикла (recursive type) этот узел просто добавляется в конце —
    /// порядок остаётся стабильным, дубликатов не возникает.
    /// </summary>
    private static IReadOnlyList<string> TopologicalSort(
        Dictionary<string, HashSet<string>> dependencies,
        HashSet<string> allTypeNames)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Visit(string typeName)
        {
            if (visiting.Contains(typeName)) return; // cycle — skip
            if (visited.Contains(typeName)) return;

            visiting.Add(typeName);

            if (dependencies.TryGetValue(typeName, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (allTypeNames.Contains(dep))
                        Visit(dep);
                }
            }

            visiting.Remove(typeName);
            visited.Add(typeName);
            result.Add(typeName);
        }

        foreach (var typeName in allTypeNames)
        {
            if (!visited.Contains(typeName))
                Visit(typeName);
        }

        return result;
    }
}
