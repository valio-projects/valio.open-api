using ValIO.Core.Interfaces;
using ValIO.Core.Models;

namespace ValIO.Core.Services;

/// <summary>
/// Резолвер путей импорта/usings для разных языков
/// </summary>
public class ImportPathResolver
{
    private readonly INamingConverter _namingConverter;

    public ImportPathResolver(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    /// <summary>
    /// Определяет путь импорта для модели
    /// </summary>
    public string? ResolveModelImport(string modelName, GenerationLanguage language, string? modelsPath = null)
    {
        if (IsBuiltInType(modelName, language))
            return null;

        return language switch
        {
            GenerationLanguage.TypeScript => ResolveTypeScriptModelImport(modelName, modelsPath),
            GenerationLanguage.CSharp => ResolveCSharpModelImport(modelName),
            _ => null
        };
    }

    /// <summary>
    /// Определяет путь импорта для enum
    /// </summary>
    public string? ResolveEnumImport(string enumName, GenerationLanguage language, string? modelsPath = null)
    {
        if (IsBuiltInType(enumName, language))
            return null;

        return language switch
        {
            GenerationLanguage.TypeScript => ResolveTypeScriptModelImport(enumName, modelsPath),
            GenerationLanguage.CSharp => ResolveCSharpModelImport(enumName),
            _ => null
        };
    }

    /// <summary>
    /// Определяет путь импорта для схемы валидации
    /// </summary>
    public string? ResolveSchemaImport(string schemaName, GenerationLanguage language, string? schemasPath = null)
    {
        if (IsBuiltInType(schemaName, language))
            return null;

        return language switch
        {
            GenerationLanguage.TypeScript => ResolveTypeScriptSchemaImport(schemaName, schemasPath),
            GenerationLanguage.CSharp => ResolveCSharpSchemaImport(schemaName),
            _ => null
        };
    }

    /// <summary>
    /// Определяет путь импорта для клиента
    /// </summary>
    public string? ResolveClientImport(string clientName, GenerationLanguage language, string? clientsPath = null)
    {
        return language switch
        {
            GenerationLanguage.TypeScript => ResolveTypeScriptClientImport(clientName, clientsPath),
            GenerationLanguage.CSharp => ResolveCSharpClientImport(clientName),
            _ => null
        };
    }

    /// <summary>
    /// Определяет путь импорта для TypeScript модели
    /// </summary>
    private string ResolveTypeScriptModelImport(string modelName, string? modelsPath)
    {
        var fileName = _namingConverter.ToCamelCase(modelName);
        var path = modelsPath ?? "./models";
        return $"{path}/{fileName}";
    }

    /// <summary>
    /// Определяет путь импорта для TypeScript схемы
    /// </summary>
    private string ResolveTypeScriptSchemaImport(string schemaName, string? schemasPath)
    {
        // Убираем суффикс "Schema" если есть
        var baseName = schemaName.EndsWith("Schema", StringComparison.OrdinalIgnoreCase)
            ? schemaName.Substring(0, schemaName.Length - 6)
            : schemaName;
        
        var fileName = _namingConverter.ToCamelCase(baseName);
        var path = schemasPath ?? "./schemas";
        return $"{path}/{fileName}";
    }

    /// <summary>
    /// Определяет путь импорта для TypeScript клиента
    /// </summary>
    private string ResolveTypeScriptClientImport(string clientName, string? clientsPath)
    {
        var fileName = _namingConverter.ToCamelCase(clientName);
        var path = clientsPath ?? "./clients";
        return $"{path}/{fileName}";
    }

    /// <summary>
    /// Определяет путь импорта для C# модели (namespace)
    /// </summary>
    private string? ResolveCSharpModelImport(string modelName)
    {
        // Для C# пути импорта определяются через namespace, который должен быть указан в TemplateData
        // Здесь возвращаем null, так как namespace должен быть указан явно
        return null;
    }

    /// <summary>
    /// Определяет путь импорта для C# схемы (namespace)
    /// </summary>
    private string? ResolveCSharpSchemaImport(string schemaName)
    {
        // Для C# пути импорта определяются через namespace, который должен быть указан в TemplateData
        // Здесь возвращаем null, так как namespace должен быть указан явно
        return null;
    }

    /// <summary>
    /// Определяет путь импорта для C# клиента (namespace)
    /// </summary>
    private string? ResolveCSharpClientImport(string clientName)
    {
        // Для C# пути импорта определяются через namespace, который должен быть указан в TemplateData
        // Здесь возвращаем null, так как namespace должен быть указан явно
        return null;
    }

    /// <summary>
    /// Проверяет, является ли тип встроенным типом языка
    /// </summary>
    private static bool IsBuiltInType(string typeName, GenerationLanguage language)
    {
        if (string.IsNullOrEmpty(typeName))
            return true;

        var builtInTypes = language switch
        {
            GenerationLanguage.CSharp => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "object", "string", "int", "long", "short", "byte", "sbyte", "uint", "ulong", "ushort",
                "float", "double", "decimal", "bool", "char", "void", "DateTime", "DateTimeOffset",
                "TimeSpan", "Guid", "Uri", "Stream", "byte[]", "List", "Dictionary", "IEnumerable",
                "ICollection", "IList", "IDictionary", "Task", "HttpResponseMessage"
            },
            GenerationLanguage.TypeScript => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "any", "unknown", "object", "string", "number", "boolean", "void", "null", "undefined",
                "Date", "Blob", "File", "Array", "Record", "Promise", "Response"
            },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };

        return builtInTypes.Contains(typeName);
    }
}
