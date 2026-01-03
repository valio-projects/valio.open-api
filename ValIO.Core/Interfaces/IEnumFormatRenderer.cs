using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для рендеринга enum'ов в разных форматах
/// </summary>
public interface IEnumFormatRenderer
{
    /// <summary>
    /// Формат enum, который поддерживает этот renderer
    /// </summary>
    EnumFormat Format { get; }
    
    /// <summary>
    /// Язык генерации, который поддерживает этот renderer
    /// </summary>
    GenerationLanguage Language { get; }
    
    /// <summary>
    /// Рендерит enum в указанном формате
    /// </summary>
    string Render(EnumTemplateData data);
}
