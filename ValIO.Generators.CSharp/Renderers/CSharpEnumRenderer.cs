using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;

namespace ValIO.Generators.CSharp.Renderers;

/// <summary>
/// Рендерер для генерации C# enum
/// </summary>
public class CSharpEnumRenderer : IEnumFormatRenderer
{
    public EnumFormat Format => EnumFormat.Enum;
    public GenerationLanguage Language => GenerationLanguage.CSharp;

    public string Render(IEnumTemplateData data)
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
        CommentGenerator.GenerateXmlComment(sb, 0, data.Description);
        
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
            CommentGenerator.GenerateXmlComment(sb, 1, value.Description);
            
            // Генерируем значение enum
            var comma = i < data.Values.Count - 1 ? "," : "";
            CodeFormatter.AppendLine(sb, 1, $"{value.Name} = {value.ValueString}{comma}");
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    // Метод EscapeXmlComment теперь в ValIO.Core.Utilities.CodeEscapeHelper
}
