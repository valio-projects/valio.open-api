using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Generators.TypeScript.Zod.Renderers;

/// <summary>
/// Рендерер для генерации Zod enum schemas
/// </summary>
public class ZodEnumSchemaRenderer : IEnumFormatRenderer
{
    private readonly INamingConverter _namingConverter;

    public ZodEnumSchemaRenderer(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    public EnumFormat Format => EnumFormat.ValidationSchema;
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    public string Render(EnumTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        var schemaName = $"{data.Name}Schema";
        var typeName = data.Name;
        var underlyingType = data.UnderlyingType ?? "string";
        var isStringEnum = underlyingType == "string";
        var constArrayName = $"{typeName}Values";
        
        sb.AppendLine("import { z } from 'zod';");
        sb.AppendLine();
        
        // Генерируем массив значений enum
        if (isStringEnum)
        {
            sb.AppendLine($"export const {constArrayName} = [");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                sb.AppendLine($"  {valueString}{comma}");
            }
            sb.AppendLine("] as const satisfies readonly string[];");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"export const {constArrayName} = [");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                sb.AppendLine($"  {valueString}{comma}");
            }
            sb.AppendLine("] as const satisfies readonly number[];");
            sb.AppendLine();
        }
        
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(data.Description))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {EscapeJSDocComment(data.Description)}");
            sb.AppendLine(" */");
        }
        
        // Генерируем Zod схему enum
        if (isStringEnum)
        {
            sb.AppendLine($"export const {schemaName} = z.enum({constArrayName});");
        }
        else
        {
            // Числовые enum'ы в Zod требуют использования z.union с z.literal
            sb.AppendLine($"export const {schemaName} = z.union([");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                sb.AppendLine($"  z.literal({valueString}){comma}");
            }
            sb.AppendLine("]);");
        }
        
        return sb.ToString();
    }

    private string FormatEnumValue(string valueString, string underlyingType)
    {
        if (underlyingType == "string")
        {
            // Если значение уже в кавычках, оставляем как есть, иначе добавляем кавычки
            if (valueString.StartsWith("\"") && valueString.EndsWith("\""))
            {
                return valueString;
            }
            return $"\"{valueString}\"";
        }
        return valueString;
    }

    private string EscapeJSDocComment(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        
        return text
            .Replace("*/", "* /")
            .Replace("/*", "/ *");
    }
}
