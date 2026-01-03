using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для рендеринга enum'ов
/// </summary>
public interface IEnumRenderer
{
    /// <summary>
    /// Рендерит enum
    /// </summary>
    string Render(EnumTemplateData data);
}
