using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Generators.TypeScript.Renderers;

/// <summary>
/// Рендерер для генерации TypeScript union types
/// </summary>
public class TypeScriptUnionTypeRenderer : IEnumFormatRenderer
{
    public EnumFormat Format => EnumFormat.Union;
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    public string Render(EnumTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(data.Description))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {EscapeJSDocComment(data.Description)}");
            sb.AppendLine(" */");
        }
        
        // Генерируем TypeScript union type
        sb.Append($"export type {data.Name} = ");
        for (int i = 0; i < data.Values.Count; i++)
        {
            var value = data.Values[i];
            if (i > 0)
            {
                sb.Append(" | ");
            }
            sb.Append(value.ValueString);
        }
        sb.AppendLine(";");
        
        return sb.ToString();
    }

    private string EscapeJSDocComment(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        
        return text
            .Replace("*/", "* /")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ");
    }
}
