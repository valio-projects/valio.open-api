using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;

namespace ValIO.Generators.TypeScript.Services;

/// <summary>
/// Анализатор зависимостей для TypeScript
/// </summary>
public class TypeScriptDependencyAnalyzer : BaseDependencyAnalyzer
{
    public TypeScriptDependencyAnalyzer(INamingConverter namingConverter)
        : base(namingConverter, GenerationLanguage.TypeScript)
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

        // Определяем, является ли это схемой валидации (по суффиксу Schema)
        if (typeName.EndsWith("Schema", StringComparison.OrdinalIgnoreCase))
        {
            var schemaName = typeName.Substring(0, typeName.Length - 6); // Убираем "Schema"
            var fileName = NamingConverter.ToCamelCase(schemaName);
            var path = schemasPath ?? "./schemas";
            return $"{path}/{fileName}";
        }

        // Обычная модель
        var modelFileName = NamingConverter.ToCamelCase(typeName);
        var modelPath = modelsPath ?? "./models";
        return $"{modelPath}/{modelFileName}";
    }

    /// <summary>
    /// Проверяет, является ли тип встроенным типом TypeScript
    /// </summary>
    public override bool IsBuiltInType(string typeName, GenerationLanguage language)
    {
        if (string.IsNullOrEmpty(typeName))
            return true;

        // Проверяем generic типы типа Record<string, any>, Array<T> и т.д.
        if (typeName.Contains('<'))
        {
            // Извлекаем базовое имя типа до '<'
            var baseTypeName = typeName.Substring(0, typeName.IndexOf('<'));
            return IsBuiltInType(baseTypeName, language);
        }

        // Проверяем массивы TypeScript типа Type[]
        if (typeName.EndsWith("[]"))
        {
            var baseTypeName = typeName.Substring(0, typeName.Length - 2);
            return IsBuiltInType(baseTypeName, language);
        }

        var builtInTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "any", "unknown", "object", "string", "number", "boolean", "void", "null", "undefined",
            "Date", "Blob", "File", "Array", "Record", "Promise", "Response"
        };

        return builtInTypes.Contains(typeName);
    }
}
