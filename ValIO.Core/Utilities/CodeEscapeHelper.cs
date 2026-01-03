namespace ValIO.Core.Utilities;

/// <summary>
/// Утилита для экранирования специальных символов в коде
/// </summary>
public static class CodeEscapeHelper
{
    /// <summary>
    /// Экранирует XML комментарии (для C#)
    /// </summary>
    public static string EscapeXmlComment(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
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
