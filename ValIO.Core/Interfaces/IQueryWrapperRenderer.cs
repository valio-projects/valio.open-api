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
    string RenderQueryHook(OperationTemplateData operation, string? clientMethodName = null, string? clientImportPath = null);
    
    /// <summary>
    /// Рендерит mutation hook для POST, PUT, DELETE, PATCH операций
    /// </summary>
    string RenderMutationHook(OperationTemplateData operation, string? clientMethodName = null, string? clientImportPath = null);
}
