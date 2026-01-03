namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для рендеринга клиентов
/// </summary>
public interface IClientRenderer
{
    /// <summary>
    /// Рендерит клиент
    /// </summary>
    string Render(IClientTemplateData data);
}
