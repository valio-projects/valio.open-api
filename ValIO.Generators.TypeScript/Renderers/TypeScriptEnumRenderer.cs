using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;

namespace ValIO.Generators.TypeScript.Renderers;

/// <summary>
/// Рендерер для генерации TypeScript enum
/// </summary>
public class TypeScriptEnumRenderer : IEnumFormatRenderer
{
    public EnumFormat Format => EnumFormat.Enum;
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    public string Render(IEnumTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
        
        // Генерируем TypeScript enum
        sb.AppendLine($"export enum {data.Name} {{");
        for (int i = 0; i < data.Values.Count; i++)
        {
            var value = data.Values[i];
            
            CommentGenerator.GenerateJSDocComment(sb, 1, value.Description);
            
            var comma = i < data.Values.Count - 1 ? "," : "";
            CodeFormatter.AppendLine(sb, 1, $"{value.Name} = {value.ValueString}{comma}");
        }
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    // Метод EscapeJSDocComment теперь в ValIO.Core.Utilities.CodeEscapeHelper
}
