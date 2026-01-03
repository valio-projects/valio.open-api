using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;

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

    public string Render(IEnumTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        var schemaName = $"{data.Name}Schema";
        var typeName = data.Name;
        var underlyingType = data.UnderlyingType ?? "string";
        var isStringEnum = underlyingType == "string";
        var constArrayName = $"{typeName}Values";
        
        CodeFormatter.AppendLine(sb, 0, "import { z } from 'zod';");
        CodeFormatter.AppendEmptyLine(sb);
        
        // Генерируем массив значений enum
        if (isStringEnum)
        {
            CodeFormatter.AppendLine(sb, 0, $"export const {constArrayName} = [");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                CodeFormatter.AppendLine(sb, 1, $"{valueString}{comma}");
            }
            CodeFormatter.AppendLine(sb, 0, "] as const satisfies readonly string[];");
            CodeFormatter.AppendEmptyLine(sb);
        }
        else
        {
            CodeFormatter.AppendLine(sb, 0, $"export const {constArrayName} = [");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                CodeFormatter.AppendLine(sb, 1, $"{valueString}{comma}");
            }
            CodeFormatter.AppendLine(sb, 0, "] as const satisfies readonly number[];");
            CodeFormatter.AppendEmptyLine(sb);
        }
        
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
        
        // Генерируем Zod схему enum
        if (isStringEnum)
        {
            CodeFormatter.AppendLine(sb, 0, $"export const {schemaName} = z.enum({constArrayName});");
        }
        else
        {
            // Числовые enum'ы в Zod требуют использования z.union с z.literal
            CodeFormatter.AppendLine(sb, 0, $"export const {schemaName} = z.union([");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                CodeFormatter.AppendLine(sb, 1, $"z.literal({valueString}){comma}");
            }
            CodeFormatter.AppendLine(sb, 0, "]);");
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

}
