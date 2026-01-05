using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Services;

/// <summary>
/// Базовый класс для анализа зависимостей между типами
/// </summary>
public abstract class BaseDependencyAnalyzer : IDependencyAnalyzer
{
    protected readonly INamingConverter NamingConverter;
    protected readonly GenerationLanguage Language;

    protected BaseDependencyAnalyzer(INamingConverter namingConverter, GenerationLanguage language)
    {
        NamingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        Language = language;
    }

    /// <summary>
    /// Анализирует зависимости модели
    /// </summary>
    public virtual IReadOnlyList<string> AnalyzeModelDependencies(ModelTemplateData model)
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
    public virtual IReadOnlyList<string> AnalyzeOperationDependencies(OperationTemplateData operation)
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
    public virtual void CollectDependenciesFromTypeInfo(TypeInfo typeInfo, HashSet<string> dependencies)
    {
        if (typeInfo == null)
            return;

        // Если это не примитивный тип и не void, добавляем базовый тип в зависимости
        if (!typeInfo.IsPrimitive && typeInfo.BaseType != "void" && !string.IsNullOrEmpty(typeInfo.BaseType))
        {
            // Проверяем, не является ли это встроенным типом языка
            if (!IsBuiltInType(typeInfo))
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
            if (!string.IsNullOrEmpty(dependency) && !IsBuiltInType(dependency, typeInfo.Language ?? Language))
            {
                dependencies.Add(dependency);
            }
        }
    }

    /// <summary>
    /// Собирает все зависимости из модели
    /// </summary>
    public virtual IReadOnlyList<string> CollectAllDependencies(ModelTemplateData model)
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
    public virtual IReadOnlyList<string> CollectAllDependencies(OperationTemplateData operation)
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
    public virtual IReadOnlyList<string> CollectAllDependencies(ClientTemplateData client)
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
    public virtual Dictionary<string, string> ResolveImportPaths(
        IReadOnlyList<string> dependencies,
        string? modelsPath = null,
        string? schemasPath = null)
    {
        var importPaths = new Dictionary<string, string>();

        foreach (var dependency in dependencies)
        {
            if (string.IsNullOrEmpty(dependency))
                continue;

            var importPath = ResolveImportPath(dependency, modelsPath, schemasPath);
            if (!string.IsNullOrEmpty(importPath))
            {
                importPaths[dependency] = importPath;
            }
        }

        return importPaths;
    }

    /// <summary>
    /// Определяет путь импорта для одного типа (язык-специфичная реализация)
    /// </summary>
    protected abstract string? ResolveImportPath(
        string typeName,
        string? modelsPath = null,
        string? schemasPath = null);

    /// <summary>
    /// Проверяет, является ли тип встроенным типом языка
    /// </summary>
    public abstract bool IsBuiltInType(string typeName, GenerationLanguage language);
    
    /// <summary>
    /// Проверяет, является ли тип встроенным типом языка на основе TypeInfo
    /// </summary>
    public bool IsBuiltInType(TypeInfo typeInfo)
    {
        if (typeInfo == null)
            return true;
        
        // Примитивные типы всегда встроенные
        if (typeInfo.IsPrimitive)
            return true;
        
        // Коллекции и словари - встроенные типы, если их базовый тип встроенный
        if (typeInfo.IsCollection || typeInfo.IsDictionary)
        {
            return IsBuiltInType(typeInfo.BaseType, typeInfo.Language ?? Language);
        }
        
        // Для остальных типов проверяем BaseType
        return IsBuiltInType(typeInfo.BaseType, typeInfo.Language ?? Language);
    }
}
