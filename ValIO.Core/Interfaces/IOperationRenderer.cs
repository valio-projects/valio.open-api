using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для рендеринга операций
/// </summary>
public interface IOperationRenderer
{
    /// <summary>
    /// Рендерит операцию
    /// </summary>
    string Render(OperationTemplateData data);
}
