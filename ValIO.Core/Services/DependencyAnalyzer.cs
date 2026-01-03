using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Services;

/// <summary>
/// Анализатор зависимостей между типами для определения импортов/usings
/// </summary>
public class DependencyAnalyzer
{
    private readonly INamingConverter _namingConverter;

    public DependencyAnalyzer(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    /// <summary>
    /// Анализирует зависимости модели
    /// </summary>
    public IReadOnlyList<string> AnalyzeModelDependencies(ModelTemplateData model)
    {
        var dependencies = new HashSet<string>();

        // Собираем зависимости из свойств
        foreach (var property in model.Properties)
        {
            CollectDependenciesFromTypeInfo(property.TypeInfo, dependencies);
        }

        // Собираем зависимости из базового класса
        if (!string.IsNullOrEmpty(model.BaseClassName))
        {
            dependencies.Add(model.BaseClassName);
        }

        // Собираем зависимости из свойств базового класса
        foreach (var property in model.BaseClassProperties)
        {
            CollectDependenciesFromTypeInfo(property.TypeInfo, dependencies);
        }

        return dependencies.ToList();
    }

    /// <summary>
    /// Анализирует зависимости операции
    /// </summary>
    public IReadOnlyList<string> AnalyzeOperationDependencies(OperationTemplateData operation)
    {
        var dependencies = new HashSet<string>();

        // Собираем зависимости из типа возврата
        CollectDependenciesFromTypeInfo(operation.ReturnTypeInfo, dependencies);

        // Собираем зависимости из request body
        if (operation.RequestBodyTypeInfo != null)
        {
            CollectDependenciesFromTypeInfo(operation.RequestBodyTypeInfo, dependencies);
        }

        // Собираем зависимости из параметров
        foreach (var parameter in operation.PathParameters)
        {
            CollectDependenciesFromTypeInfo(parameter.TypeInfo, dependencies);
        }

        foreach (var parameter in operation.QueryParameters)
        {
            CollectDependenciesFromTypeInfo(parameter.TypeInfo, dependencies);
        }

        foreach (var parameter in operation.HeaderParameters)
        {
            CollectDependenciesFromTypeInfo(parameter.TypeInfo, dependencies);
        }

        // Собираем зависимости из form-data свойств
        if (operation.FormDataProperties != null)
        {
            foreach (var formDataProperty in operation.FormDataProperties)
            {
                CollectDependenciesFromTypeInfo(formDataProperty.TypeInfo, dependencies);
            }
        }

        // Собираем зависимости из ответов
        foreach (var response in operation.Responses)
        {
            if (response.ResponseTypeInfo != null)
            {
                CollectDependenciesFromTypeInfo(response.ResponseTypeInfo, dependencies);
            }
        }

        // Собираем зависимости из модели query параметров
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            dependencies.Add(operation.QueryParametersModelTypeName);
        }

        // Собираем зависимости из схем валидации
        if (!string.IsNullOrEmpty(operation.RequestSchemaName))
        {
            dependencies.Add(operation.RequestSchemaName);
        }

        if (!string.IsNullOrEmpty(operation.ResponseSchemaName))
        {
            dependencies.Add(operation.ResponseSchemaName);
        }

        return dependencies.ToList();
    }

    /// <summary>
    /// Собирает все зависимости из TypeInfo
    /// </summary>
    public void CollectDependenciesFromTypeInfo(TypeInfo typeInfo, HashSet<string> dependencies)
    {
        if (typeInfo == null)
            return;

        // Если это не примитивный тип и не void, добавляем базовый тип в зависимости
        if (!typeInfo.IsPrimitive && typeInfo.BaseType != "void" && !string.IsNullOrEmpty(typeInfo.BaseType))
        {
            // Проверяем, не является ли это встроенным типом языка
            if (!IsBuiltInType(typeInfo.BaseType, typeInfo.Language))
            {
                dependencies.Add(typeInfo.BaseType);
            }
        }

        // Собираем зависимости из generic аргументов
        foreach (var genericArg in typeInfo.GenericArguments)
        {
            CollectDependenciesFromTypeInfo(genericArg, dependencies);
        }

        // Собираем зависимости из списка зависимостей TypeInfo
        foreach (var dependency in typeInfo.Dependencies)
        {
            if (!string.IsNullOrEmpty(dependency) && !IsBuiltInType(dependency, typeInfo.Language))
            {
                dependencies.Add(dependency);
            }
        }
    }

    /// <summary>
    /// Собирает все зависимости из модели
    /// </summary>
    public IReadOnlyList<string> CollectAllDependencies(ModelTemplateData model)
    {
        var dependencies = new HashSet<string>();
        var modelDependencies = AnalyzeModelDependencies(model);
        
        foreach (var dependency in modelDependencies)
        {
            dependencies.Add(dependency);
        }

        // Добавляем зависимости из списка зависимостей модели
        foreach (var dependency in model.Dependencies)
        {
            dependencies.Add(dependency);
        }

        return dependencies.ToList();
    }

    /// <summary>
    /// Собирает все зависимости из операции
    /// </summary>
    public IReadOnlyList<string> CollectAllDependencies(OperationTemplateData operation)
    {
        var dependencies = new HashSet<string>();
        var operationDependencies = AnalyzeOperationDependencies(operation);
        
        foreach (var dependency in operationDependencies)
        {
            dependencies.Add(dependency);
        }

        // Добавляем зависимости из списка зависимостей операции
        foreach (var dependency in operation.Dependencies)
        {
            dependencies.Add(dependency);
        }

        return dependencies.ToList();
    }

    /// <summary>
    /// Собирает все зависимости из клиента
    /// </summary>
    public IReadOnlyList<string> CollectAllDependencies(ClientTemplateData client)
    {
        var dependencies = new HashSet<string>();

        // Собираем зависимости из всех операций
        foreach (var operation in client.Operations)
        {
            // Operations в ClientTemplateData может быть типизированным, приводим к базовому типу
            // Базовая версия OperationTemplateData наследуется от generic, поэтому можем привести через object
            OperationTemplateData baseOperation = operation is OperationTemplateData nonGeneric ? 
                nonGeneric : (OperationTemplateData)(object)operation;
            var operationDependencies = CollectAllDependencies(baseOperation);
            foreach (var dependency in operationDependencies)
            {
                dependencies.Add(dependency);
            }
        }

        // Добавляем зависимости из списка зависимостей клиента
        foreach (var dependency in client.Dependencies)
        {
            dependencies.Add(dependency);
        }

        return dependencies.ToList();
    }

    /// <summary>
    /// Определяет пути импорта для зависимостей
    /// </summary>
    public Dictionary<string, string> ResolveImportPaths(
        IReadOnlyList<string> dependencies,
        GenerationLanguage language,
        string? modelsPath = null,
        string? schemasPath = null)
    {
        var importPaths = new Dictionary<string, string>();

        foreach (var dependency in dependencies)
        {
            if (string.IsNullOrEmpty(dependency))
                continue;

            var importPath = ResolveImportPath(dependency, language, modelsPath, schemasPath);
            if (!string.IsNullOrEmpty(importPath))
            {
                importPaths[dependency] = importPath;
            }
        }

        return importPaths;
    }

    /// <summary>
    /// Определяет путь импорта для одного типа
    /// </summary>
    private string? ResolveImportPath(
        string typeName,
        GenerationLanguage language,
        string? modelsPath = null,
        string? schemasPath = null)
    {
        if (IsBuiltInType(typeName, language))
            return null;

        return language switch
        {
            GenerationLanguage.TypeScript => ResolveTypeScriptImportPath(typeName, modelsPath, schemasPath),
            GenerationLanguage.CSharp => ResolveCSharpImportPath(typeName),
            _ => null
        };
    }

    /// <summary>
    /// Определяет путь импорта для TypeScript
    /// </summary>
    private string ResolveTypeScriptImportPath(string typeName, string? modelsPath, string? schemasPath)
    {
        // Определяем, является ли это схемой валидации (по суффиксу Schema)
        if (typeName.EndsWith("Schema", StringComparison.OrdinalIgnoreCase))
        {
            var schemaName = typeName.Substring(0, typeName.Length - 6); // Убираем "Schema"
            var fileName = _namingConverter.ToCamelCase(schemaName);
            var path = schemasPath ?? "./schemas";
            return $"{path}/{fileName}";
        }

        // Обычная модель
        var modelFileName = _namingConverter.ToCamelCase(typeName);
        var modelPath = modelsPath ?? "./models";
        return $"{modelPath}/{modelFileName}";
    }

    /// <summary>
    /// Определяет путь импорта для C# (namespace)
    /// </summary>
    private string? ResolveCSharpImportPath(string typeName)
    {
        // Для C# пути импорта определяются через namespace, который должен быть указан в TemplateData
        // Здесь возвращаем null, так как namespace должен быть указан явно
        return null;
    }

    /// <summary>
    /// Проверяет, является ли тип встроенным типом языка
    /// </summary>
    private static bool IsBuiltInType(string typeName, GenerationLanguage? language)
    {
        if (string.IsNullOrEmpty(typeName))
            return true;

        // Проверяем generic типы типа Record<string, any>, List<T>, Dictionary<K,V> и т.д.
        if (typeName.Contains('<'))
        {
            // Извлекаем базовое имя типа до '<'
            var baseTypeName = typeName.Substring(0, typeName.IndexOf('<'));
            return IsBuiltInType(baseTypeName, language);
        }

        // Проверяем массивы TypeScript типа Type[]
        if (language == GenerationLanguage.TypeScript && typeName.EndsWith("[]"))
        {
            var baseTypeName = typeName.Substring(0, typeName.Length - 2);
            return IsBuiltInType(baseTypeName, language);
        }

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
