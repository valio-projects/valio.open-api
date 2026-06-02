using System.Text;
using ValIO.Core.Utilities;

namespace ValIO.Core.Utilities;

/// <summary>
/// Утилита для генерации комментариев в коде
/// </summary>
public static class CommentGenerator
{
    /// <summary>
    /// Генерирует XML комментарий для C#
    /// </summary>
    public static void GenerateXmlComment(StringBuilder sb, int indentLevel, string? description)
    {
        if (string.IsNullOrEmpty(description))
            return;
        
        CodeFormatter.AppendLine(sb, indentLevel, "/// <summary>");
        
        // Разбиваем многострочные комментарии на отдельные строки
        var lines = description.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            var escapedLine = CodeEscapeHelper.EscapeXmlComment(line);
            CodeFormatter.AppendLine(sb, indentLevel, $"/// {escapedLine}");
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, "/// </summary>");
    }
    
    /// <summary>
    /// Генерирует XML комментарий с параметрами для C#
    /// </summary>
    public static void GenerateXmlCommentWithParams(StringBuilder sb, int indentLevel, string? description, IReadOnlyList<(string Name, string? Description)>? parameters = null, string? returns = null)
    {
        if (string.IsNullOrEmpty(description) && (parameters == null || parameters.Count == 0) && string.IsNullOrEmpty(returns))
            return;
        
        CodeFormatter.AppendLine(sb, indentLevel, "/// <summary>");
        if (!string.IsNullOrEmpty(description))
        {
            // Разбиваем многострочные комментарии на отдельные строки
            var lines = description.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var escapedLine = CodeEscapeHelper.EscapeXmlComment(line);
                CodeFormatter.AppendLine(sb, indentLevel, $"/// {escapedLine}");
            }
        }
        CodeFormatter.AppendLine(sb, indentLevel, "/// </summary>");
        
        if (parameters != null && parameters.Count > 0)
        {
            foreach (var param in parameters)
            {
                if (!string.IsNullOrEmpty(param.Description))
                {
                    CodeFormatter.AppendLine(sb, indentLevel, $"/// <param name=\"{param.Name}\">{CodeEscapeHelper.EscapeXmlComment(param.Description)}</param>");
                }
                else
                {
                    CodeFormatter.AppendLine(sb, indentLevel, $"/// <param name=\"{param.Name}\"></param>");
                }
            }
        }
        
        if (!string.IsNullOrEmpty(returns))
        {
            CodeFormatter.AppendLine(sb, indentLevel, $"/// <returns>{CodeEscapeHelper.EscapeXmlComment(returns)}</returns>");
        }
    }
    
    /// <summary>
    /// Генерирует JSDoc комментарий для TypeScript
    /// </summary>
    public static void GenerateJSDocComment(StringBuilder sb, int indentLevel, string? description)
    {
        if (string.IsNullOrEmpty(description))
            return;

        CodeFormatter.AppendLine(sb, indentLevel, "/**");
        // Разбиваем многострочные комментарии (например, несколько @tag-строк) на отдельные ` * ` строки.
        var lines = description.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            CodeFormatter.AppendLine(sb, indentLevel, $" * {CodeEscapeHelper.EscapeJSDocComment(line)}");
        }
        CodeFormatter.AppendLine(sb, indentLevel, " */");
    }
    
    /// <summary>
    /// Генерирует JSDoc комментарий с параметрами для TypeScript
    /// </summary>
    public static void GenerateJSDocCommentWithParams(StringBuilder sb, int indentLevel, string? description, IReadOnlyList<(string Name, string? Description, string? Type)>? parameters = null, string? returns = null)
    {
        if (string.IsNullOrEmpty(description) && (parameters == null || parameters.Count == 0) && string.IsNullOrEmpty(returns))
            return;
        
        CodeFormatter.AppendLine(sb, indentLevel, "/**");
        if (!string.IsNullOrEmpty(description))
        {
            CodeFormatter.AppendLine(sb, indentLevel, $" * {CodeEscapeHelper.EscapeJSDocComment(description)}");
        }
        
        if (parameters != null && parameters.Count > 0)
        {
            foreach (var param in parameters)
            {
                var typePart = !string.IsNullOrEmpty(param.Type) ? $"{{{param.Type}}}" : "";
                var descPart = !string.IsNullOrEmpty(param.Description) ? $" {CodeEscapeHelper.EscapeJSDocComment(param.Description)}" : "";
                CodeFormatter.AppendLine(sb, indentLevel, $" * @param {typePart} {param.Name}{descPart}");
            }
        }
        
        if (!string.IsNullOrEmpty(returns))
        {
            CodeFormatter.AppendLine(sb, indentLevel, $" * @returns {CodeEscapeHelper.EscapeJSDocComment(returns)}");
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, " */");
    }
}
