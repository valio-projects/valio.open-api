namespace ValIO.Generators.CSharp.Helpers;

/// <summary>
/// Helper класс для шаблонов RazorLight
/// Предоставляет методы для вывода текста без HTML-кодирования
/// </summary>
public static class TemplateHelper
{
    /// <summary>
    /// Возвращает текст без изменений (для обхода HTML-кодирования в RazorLight)
    /// </summary>
    public static string Raw(string? text)
    {
        return text ?? string.Empty;
    }
}
