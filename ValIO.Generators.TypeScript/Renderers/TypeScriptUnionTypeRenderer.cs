using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;

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
        CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
        
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

    // Метод EscapeJSDocComment теперь в ValIO.Core.Utilities.CodeEscapeHelper
}
