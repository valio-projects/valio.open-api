using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ValIO.Core.Utilities;

/// <summary>
/// Утилита для генерации комментариев в коде
/// </summary>
public static class CommentGenerator
{
    /// <summary>Recognizes complete inline documentation elements without accepting arbitrary XML markup.</summary>
    private static readonly Regex InlineXmlDocumentation = new(
        @"<(?:see|seealso|paramref|typeparamref)\s+[^<>]*/>|<c>[^<]*</c>|<code>[^<]*</code>",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    /// <summary>Writes a model or member summary while preserving inline XML documentation.</summary>
    /// <param name="sb">The generated source buffer.</param>
    /// <param name="indentLevel">The enclosing indentation depth.</param>
    /// <param name="description">The schema description to write as a summary.</param>
    public static void GenerateXmlComment(StringBuilder sb, int indentLevel, string? description)
    {
        if (!string.IsNullOrWhiteSpace(description))
            AppendXmlElement(sb, indentLevel, "<summary>", "</summary>", description, multiline: true);
    }
    
    /// <summary>Writes XML documentation for a declaration with arguments and an optional return value.</summary>
    /// <param name="sb">The generated source buffer.</param>
    /// <param name="indentLevel">The enclosing indentation depth.</param>
    /// <param name="description">The declaration summary.</param>
    /// <param name="parameters">The emitted parameter names and descriptions.</param>
    /// <param name="returns">The return value description.</param>
    public static void GenerateXmlCommentWithParams(StringBuilder sb, int indentLevel, string? description,
        IReadOnlyList<(string Name, string? Description)>? parameters = null, string? returns = null) =>
        GenerateXmlCommentWithParams(sb, indentLevel, description, parameters, returns, remarks: null);

    /// <summary>Writes declaration documentation and preserves multiline OpenAPI descriptions.</summary>
    /// <param name="sb">The generated source buffer.</param>
    /// <param name="indentLevel">The enclosing indentation depth.</param>
    /// <param name="description">The declaration summary.</param>
    /// <param name="parameters">The emitted parameter names and descriptions.</param>
    /// <param name="returns">The return value description.</param>
    /// <param name="remarks">Additional details accompanying the summary.</param>
    public static void GenerateXmlCommentWithParams(StringBuilder sb, int indentLevel, string? description,
        IReadOnlyList<(string Name, string? Description)>? parameters, string? returns, string? remarks)
    {
        if (!string.IsNullOrWhiteSpace(description))
            AppendXmlElement(sb, indentLevel, "<summary>", "</summary>", description, multiline: true);
        if (!string.IsNullOrWhiteSpace(remarks))
            AppendXmlElement(sb, indentLevel, "<remarks>", "</remarks>", remarks, multiline: true);
        if (parameters is not null)
        {
            foreach (var (name, text) in parameters)
            {
                var identifier = SecurityElement.Escape(name.TrimStart('@'));
                AppendXmlElement(sb, indentLevel, $"<param name=\"{identifier}\">", "</param>", text ?? "");
            }
        }

        if (!string.IsNullOrWhiteSpace(returns))
            AppendXmlElement(sb, indentLevel, "<returns>", "</returns>", returns);
    }

    /// <summary>Emits escaped XML text with a comment prefix on each physical line.</summary>
    /// <param name="sb">The generated source buffer.</param>
    /// <param name="indentLevel">The enclosing indentation depth.</param>
    /// <param name="openingTag">The formatted opening XML tag.</param>
    /// <param name="closingTag">The matching closing XML tag.</param>
    /// <param name="text">The OpenAPI text to place inside the element.</param>
    /// <param name="multiline">Whether to put the tags on separate lines even for single-line text.</param>
    private static void AppendXmlElement(StringBuilder sb, int indentLevel, string openingTag, string closingTag,
        string text, bool multiline = false)
    {
        var escapedText = EscapeXmlDocumentationText(text);
        var lines = escapedText.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        if (!multiline && lines.Length == 1)
        {
            CodeFormatter.AppendLine(sb, indentLevel, $"/// {openingTag}{escapedText}{closingTag}");
            return;
        }

        CodeFormatter.AppendLine(sb, indentLevel, $"/// {openingTag}");
        foreach (var line in lines)
            CodeFormatter.AppendLine(sb, indentLevel, $"/// {line}");
        CodeFormatter.AppendLine(sb, indentLevel, $"/// {closingTag}");
    }

    /// <summary>Preserves complete inline XML documentation elements and escapes the surrounding OpenAPI text.</summary>
    /// <param name="text">Documentation that may contain inline tags or literal angle brackets.</param>
    /// <returns>Well-formed XML content suitable for a documentation element.</returns>
    private static string EscapeXmlDocumentationText(string text)
    {
        var escaped = new StringBuilder();
        var start = 0;
        foreach (Match match in InlineXmlDocumentation.Matches(text))
        {
            escaped.Append(SecurityElement.Escape(text[start..match.Index]));
            try
            {
                var element = XElement.Parse(match.Value, LoadOptions.PreserveWhitespace);
                escaped.Append(element.ToString(SaveOptions.DisableFormatting));
            }
            catch (XmlException)
            {
                escaped.Append(SecurityElement.Escape(match.Value));
            }

            start = match.Index + match.Length;
        }

        return escaped.Append(SecurityElement.Escape(text[start..])).ToString();
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
