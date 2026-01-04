using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;

namespace ValIO.Generators.CSharp.Services;

/// <summary>
/// Анализатор зависимостей для C#
/// </summary>
public class CSharpDependencyAnalyzer : BaseDependencyAnalyzer
{
    public CSharpDependencyAnalyzer(INamingConverter namingConverter)
        : base(namingConverter, GenerationLanguage.CSharp)
    {
    }

    /// <summary>
    /// Определяет путь импорта для одного типа
    /// </summary>
    protected override string? ResolveImportPath(
        string typeName,
        string? modelsPath = null,
        string? schemasPath = null)
    {
        if (IsBuiltInType(typeName, Language))
            return null;

        // Для C# пути импорта определяются через namespace, который должен быть указан в TemplateData
        // Здесь возвращаем null, так как namespace должен быть указан явно
        return null;
    }

    /// <summary>
    /// Проверяет, является ли тип встроенным типом C#
    /// </summary>
    public override bool IsBuiltInType(string typeName, GenerationLanguage language)
    {
        if (string.IsNullOrEmpty(typeName))
            return true;

        // Проверяем generic типы типа List<T>, Dictionary<K,V> и т.д.
        if (typeName.Contains('<'))
        {
            // Извлекаем базовое имя типа до '<'
            var baseTypeName = typeName.Substring(0, typeName.IndexOf('<'));
            return IsBuiltInType(baseTypeName, language);
        }

        var builtInTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "object", "string", "int", "long", "short", "byte", "sbyte", "uint", "ulong", "ushort",
            "float", "double", "decimal", "bool", "char", "void", "DateTime", "DateTimeOffset",
            "TimeSpan", "Guid", "Uri", "Stream", "byte[]", "List", "Dictionary", "IEnumerable",
            "ICollection", "IList", "IDictionary", "Task", "HttpResponseMessage"
        };

        return builtInTypes.Contains(typeName);
    }
}
