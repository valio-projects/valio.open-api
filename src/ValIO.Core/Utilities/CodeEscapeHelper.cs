using System.Linq;
using System.Text.RegularExpressions;

namespace ValIO.Core.Utilities;

/// <summary>
/// Утилита для экранирования специальных символов в коде
/// </summary>
public static class CodeEscapeHelper
{
    /// <summary>
    /// Экранирует XML комментарии (для C#)
    /// Не экранирует валидные XML-теги типа &lt;see&gt;, &lt;param&gt;, &lt;returns&gt; и т.д.
    /// </summary>
    public static string EscapeXmlComment(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        
        // Сначала заменяем & (но избегаем замены уже экранированных последовательностей)
        text = text.Replace("&", "&amp;");
        text = text.Replace("&amp;amp;", "&amp;");
        text = text.Replace("&amp;lt;", "&lt;");
        text = text.Replace("&amp;gt;", "&gt;");
        
        // Используем регулярное выражение для поиска валидных XML-тегов
        // Паттерн находит открывающие и закрывающие теги типа <see>, </see>, <param>, </param> и т.д.
        var validXmlTagPattern = @"<(?:(?:/)?(?:see|param|returns|c|code|list|item|term|description|para|value|typeparam|exception|inheritdoc|include|permission)(?:\s[^>]*)?/?>)";
        
        // Заменяем валидные XML-теги на плейсхолдеры
        var placeholders = new System.Collections.Generic.Dictionary<string, string>();
        int placeholderIndex = 0;
        
        var regex = new Regex(validXmlTagPattern, RegexOptions.IgnoreCase);
        var matches = regex.Matches(text).Cast<Match>().Reverse().ToList();
        
        var result = text;
        foreach (var match in matches)
        {
            var placeholder = $"__XML_TAG_{placeholderIndex}__";
            placeholders[placeholder] = match.Value;
            result = result.Substring(0, match.Index) + placeholder + result.Substring(match.Index + match.Length);
            placeholderIndex++;
        }
        
        // Экранируем оставшиеся < и >
        result = result.Replace("<", "&lt;").Replace(">", "&gt;");
        
        // Восстанавливаем валидные XML-теги
        foreach (var kvp in placeholders)
        {
            result = result.Replace(kvp.Key, kvp.Value);
        }
        
        // Восстанавливаем < и > в валидных тегах (где они были заменены на &lt; и &gt;)
        result = result.Replace("&amp;lt;", "<").Replace("&amp;gt;", ">");
        
        return result;
    }
    
    /// <summary>
    /// Экранирует JSDoc комментарии (для TypeScript)
    /// </summary>
    public static string EscapeJSDocComment(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        
        return text
            .Replace("*/", "* /")
            .Replace("/*", "/ *")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ");
    }
    
    /// <summary>
    /// Экранирует строку для использования в коде (C#)
    /// </summary>
    public static string EscapeString(string? text)
    {
        if (text == null)
            return "null";
        
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
    
    /// <summary>
    /// Экранирует строку для использования в TypeScript коде
    /// </summary>
    public static string EscapeTypeScriptString(string? text)
    {
        if (text == null)
            return "null";
        
        return text
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("`", "\\`");
    }
}
