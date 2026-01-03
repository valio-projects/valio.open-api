using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Generators.CSharp.Renderers;

/// <summary>
/// Рендерер для генерации C# enum
/// </summary>
public class CSharpEnumRenderer : IEnumFormatRenderer
{
    public EnumFormat Format => EnumFormat.Enum;
    public GenerationLanguage Language => GenerationLanguage.CSharp;

    public string Render(EnumTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Генерируем namespace
        if (!string.IsNullOrEmpty(data.Namespace))
        {
            sb.AppendLine($"namespace {data.Namespace};");
            sb.AppendLine();
        }
        
        // Генерируем XML комментарий
        if (!string.IsNullOrEmpty(data.Description))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {EscapeXmlComment(data.Description)}");
            sb.AppendLine("/// </summary>");
        }
        
        // Генерируем объявление enum
        var underlyingType = data.UnderlyingType ?? "int";
        var underlyingTypeStr = underlyingType switch
        {
            "int" => "int",
            "long" => "long",
            "byte" => "byte",
            "short" => "short",
            "uint" => "uint",
            "ulong" => "ulong",
            "sbyte" => "sbyte",
            "ushort" => "ushort",
            _ => "int"
        };
        
        sb.AppendLine($"public enum {data.Name} : {underlyingTypeStr}");
        sb.AppendLine("{");
        
        // Генерируем значения enum
        for (int i = 0; i < data.Values.Count; i++)
        {
            var value = data.Values[i];
            
            // Генерируем XML комментарий для значения
            if (!string.IsNullOrEmpty(value.Description))
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// {EscapeXmlComment(value.Description)}");
                sb.AppendLine("    /// </summary>");
            }
            
            // Генерируем значение enum
            var comma = i < data.Values.Count - 1 ? "," : "";
            sb.AppendLine($"    {value.Name} = {value.ValueString}{comma}");
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    private string EscapeXmlComment(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
