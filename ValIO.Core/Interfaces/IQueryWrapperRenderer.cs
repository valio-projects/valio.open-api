using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для рендеринга query wrapper хуков (TanStack Query, SWR и т.д.)
/// </summary>
public interface IQueryWrapperRenderer
{
    /// <summary>
    /// Библиотека query wrapper (TanStackQuery, SWR и т.д.)
    /// </summary>
    QueryWrapperLibrary Library { get; }
    
    /// <summary>
    /// Язык генерации
    /// </summary>
    GenerationLanguage Language { get; }
    
    /// <summary>
    /// Рендерит query hook для GET операций
    /// </summary>
    /// <param name="operation">Операция для рендеринга</param>
    /// <param name="clientMethodName">Имя метода клиента (если null, генерируется автоматически)</param>
    /// <param name="clientImportPath">Путь для импорта клиента</param>
    /// <param name="skipImports">Пропустить генерацию импортов (импорты генерируются отдельно)</param>
    string RenderQueryHook(OperationTemplateData operation, string? clientMethodName = null, string? clientImportPath = null, bool skipImports = false);
    
    /// <summary>
    /// Рендерит mutation hook для POST, PUT, DELETE, PATCH операций
    /// </summary>
    /// <param name="operation">Операция для рендеринга</param>
    /// <param name="clientMethodName">Имя метода клиента (если null, генерируется автоматически)</param>
    /// <param name="clientImportPath">Путь для импорта клиента</param>
    /// <param name="skipImports">Пропустить генерацию импортов (импорты генерируются отдельно)</param>
    string RenderMutationHook(OperationTemplateData operation, string? clientMethodName = null, string? clientImportPath = null, bool skipImports = false);
    
    /// <summary>
    /// Собирает все импорты моделей для операции (без рендеринга)
    /// </summary>
    /// <param name="operation">Операция для сбора импортов</param>
    /// <param name="modelsPath">Путь к моделям</param>
    /// <returns>Множество имен типов моделей, которые нужно импортировать</returns>
    HashSet<string> CollectModelImports(OperationTemplateData operation, string? modelsPath);
    
    /// <summary>
    /// Собирает имя метода клиента для операции
    /// </summary>
    /// <param name="operation">Операция</param>
    /// <returns>Имя метода клиента в camelCase</returns>
    string? CollectClientMethodName(OperationTemplateData operation);
}
