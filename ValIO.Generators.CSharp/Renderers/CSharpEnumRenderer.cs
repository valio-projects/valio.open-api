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
            
            // Для C# enum с underlying type int/byte/short/long значения должны быть числами
            // Если underlying type string, то C# не поддерживает строковые enum напрямую
            // Используем числовые значения (0, 1, 2...) для всех числовых underlying types
            var enumValueString = FormatCSharpEnumValue(value, underlyingTypeStr, i);
            
            var comma = i < data.Values.Count - 1 ? "," : "";
            CodeFormatter.AppendLine(sb, 1, $"{value.Name} = {enumValueString}{comma}");
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Форматирует значение enum для C# с учетом underlying type
    /// </summary>
    private string FormatCSharpEnumValue(EnumValueTemplateData value, string underlyingType, int index)
    {
        // Если underlying type это числовой тип (int, long, byte, и т.д.), используем числовые значения
        if (IsNumericUnderlyingType(underlyingType))
        {
            // Используем индекс как значение (0, 1, 2...)
            // Или пытаемся преобразовать исходное значение в число
            if (value.Value != null && TryParseNumericValue(value.Value, underlyingType, out var numericValue))
            {
                return numericValue;
            }
            
            // Если не удалось преобразовать, используем индекс
            return index.ToString();
        }
        
        // Если underlying type string, C# не поддерживает строковые enum напрямую
        // Используем числовые значения (0, 1, 2...) и добавляем атрибут [EnumMember] для сериализации
        // Но для простоты используем числовые значения
        return index.ToString();
    }
    
    /// <summary>
    /// Проверяет, является ли underlying type числовым
    /// </summary>
    private static bool IsNumericUnderlyingType(string underlyingType)
    {
        return underlyingType switch
        {
            "int" => true,
            "long" => true,
            "byte" => true,
            "short" => true,
            "uint" => true,
            "ulong" => true,
            "sbyte" => true,
            "ushort" => true,
            _ => false
        };
    }
    
    /// <summary>
    /// Пытается преобразовать значение в число для указанного underlying type
    /// </summary>
    private static bool TryParseNumericValue(object value, string underlyingType, out string numericString)
    {
        numericString = string.Empty;
        
        if (value == null)
            return false;
        
        // Если значение уже число, используем его
        if (value is int intValue)
        {
            numericString = intValue.ToString();
            return true;
        }
        
        if (value is long longValue)
        {
            numericString = longValue.ToString();
            return true;
        }
        
        if (value is byte byteValue)
        {
            numericString = byteValue.ToString();
            return true;
        }
        
        if (value is short shortValue)
        {
            numericString = shortValue.ToString();
            return true;
        }
        
        // Если значение строка, пытаемся преобразовать в число
        if (value is string stringValue)
        {
            if (int.TryParse(stringValue, out var parsedInt))
            {
                numericString = parsedInt.ToString();
                return true;
            }
        }
        
        return false;
    }
}
