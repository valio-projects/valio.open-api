using ValIO.Core.Interfaces;
using ValIO.Core.IR;

namespace ValIO.Generators.Python.Steps;

/// <summary>
/// Утилиты, используемые несколькими Python-шагами.
/// </summary>
internal static class PythonStepHelper
{
    /// <summary>Маппит имя IR-модели на относительный Python-модуль (используется из соседнего файла models/).</summary>
    public static IReadOnlyDictionary<string, string> BuildModelModuleMap(IrDocument doc, INamingConverter naming)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var model in doc.Models)
            map[model.Name] = $".{naming.ToSnakeCase(model.Name)}";
        return map;
    }

    public static IReadOnlyDictionary<string, string> BuildEnumModuleMap(IrDocument doc, INamingConverter naming)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var irEnum in doc.Enums)
            map[irEnum.Name] = $".{naming.ToSnakeCase(irEnum.Name)}";
        return map;
    }

    /// <summary>Используется HTTPX-клиентом для импорта моделей из соседнего пакета.</summary>
    public static IReadOnlyDictionary<string, string> BuildModelModuleMapForClient(IrDocument doc, INamingConverter naming)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var model in doc.Models)
            map[model.Name] = $"..models.{naming.ToSnakeCase(model.Name)}";
        return map;
    }

    public static IReadOnlyDictionary<string, string> BuildEnumModuleMapForClient(IrDocument doc, INamingConverter naming)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var irEnum in doc.Enums)
            map[irEnum.Name] = $"..{naming.ToSnakeCase(irEnum.Name)}";
        // FIX: enums are in /models/ too.
        foreach (var irEnum in doc.Enums)
            map[irEnum.Name] = $"..models.{naming.ToSnakeCase(irEnum.Name)}";
        return map;
    }
}
