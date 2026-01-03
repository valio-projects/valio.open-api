using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для рендеринга моделей
/// </summary>
public interface IModelRenderer
{
    /// <summary>
    /// Рендерит модель
    /// </summary>
    string Render(ModelTemplateData data);
}
