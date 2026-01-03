using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для рендеринга моделей в разных форматах
/// </summary>
public interface IModelFormatRenderer
{
    /// <summary>
    /// Формат модели, который поддерживает этот renderer
    /// </summary>
    ModelFormat Format { get; }
    
    /// <summary>
    /// Язык генерации, который поддерживает этот renderer
    /// </summary>
    GenerationLanguage Language { get; }
    
    /// <summary>
    /// Рендерит модель в указанном формате
    /// </summary>
    string Render(IModelTemplateData data);
}
