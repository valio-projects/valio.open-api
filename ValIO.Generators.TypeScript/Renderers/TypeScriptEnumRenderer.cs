using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Generators.TypeScript.Renderers;

/// <summary>
/// Рендерер для генерации TypeScript enum
/// </summary>
public class TypeScriptEnumRenderer : IEnumFormatRenderer
{
    public EnumFormat Format => EnumFormat.Enum;
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
        
        // Генерируем TypeScript enum
        sb.AppendLine($"export enum {data.Name} {{");
        for (int i = 0; i < data.Values.Count; i++)
        {
            var value = data.Values[i];
            
            if (!string.IsNullOrEmpty(value.Description))
            {
                sb.AppendLine("  /**");
                sb.AppendLine($"   * {EscapeJSDocComment(value.Description)}");
                sb.AppendLine("   */");
            }
            
            var comma = i < data.Values.Count - 1 ? "," : "";
            sb.AppendLine($"  {value.Name} = {value.ValueString}{comma}");
        }
        sb.AppendLine("}");
        
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
