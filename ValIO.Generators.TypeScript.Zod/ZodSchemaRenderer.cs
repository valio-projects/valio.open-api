using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Zod.Models;

namespace ValIO.Generators.TypeScript.Zod;

/// <summary>
/// Рендерер для генерации Zod схем валидации через StringBuilders
/// </summary>
public class ZodSchemaRenderer : IModelFormatRenderer
{
    private readonly INamingConverter _namingConverter;
    private readonly ZodTypeMapper _zodTypeMapper;

    public ZodSchemaRenderer(
        INamingConverter namingConverter,
        ZodTypeMapper zodTypeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _zodTypeMapper = zodTypeMapper ?? throw new ArgumentNullException(nameof(zodTypeMapper));
    }

    public ModelFormat Format => ModelFormat.ValidationSchema;
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    /// <summary>
    /// Рендерит Zod схему для модели
    /// </summary>
    public string Render(IModelTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder(4096);
        
        var schemaName = $"{data.Name}Schema";
        var typeName = data.Name;
        
        // Генерируем импорты
        sb.AppendLine("import { z } from 'zod';");
        
        // Получаем типизированные Options через приведение через object
        ZodModelTemplateDataOptions? zodOptions;
        try
        {
            var typedData = (ModelTemplateData<ZodModelTemplateDataOptions, ZodPropertyTemplateDataOptions>)(object)data;
            zodOptions = typedData.Options;
        }
        catch
        {
            zodOptions = new ZodModelTemplateDataOptions();
        }
        
        // Генерируем импорты зависимостей схем
        var schemaDependencies = zodOptions.SchemaDependencies ?? Array.Empty<string>();
        var enumDependencies = zodOptions.EnumDependencies ?? Array.Empty<string>();
        
        if (schemaDependencies.Count > 0 || enumDependencies.Count > 0)
        {
            var allDependencies = schemaDependencies.Concat(enumDependencies)
                .Where(dep => !dep.Equals(schemaName, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            
            foreach (var dep in allDependencies)
            {
                var depFileName = SchemaNameToFileName(dep);
                sb.AppendLine($"import {{ {dep} }} from './{depFileName}.schema';");
            }
            sb.AppendLine();
        }
        
        // Проверяем наличие циклических ссылок
        var hasCircularReference = zodOptions.HasCircularReference;
        var typeDependencies = zodOptions.TypeDependencies ?? Array.Empty<string>();
        
        if (hasCircularReference)
        {
            // Для рекурсивных схем генерируем TypeScript интерфейс
            if (typeDependencies.Count > 0)
            {
                sb.AppendLine("// Импортируем TypeScript типы из .model.ts файлов для описания интерфейса");
                foreach (var typeDep in typeDependencies.Distinct().OrderBy(x => x))
                {
                    var typeFileName = TypeNameToFileName(typeDep);
                    sb.AppendLine($"import type {{ {typeDep} }} from './{typeFileName}.model';");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("// TypeScript интерфейс для строгой типизации рекурсивной схемы");
            CodeFormatter.AppendLine(sb, 0, $"type {typeName} = {{");
            foreach (var property in data.Properties)
            {
                GeneratePropertyType(sb, property, indentLevel: 1);
            }
            CodeFormatter.AppendLine(sb, 0, "};");
            CodeFormatter.AppendEmptyLine(sb);
        }
        
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
        
        // Генерируем Zod схему
        if (hasCircularReference)
        {
            CodeFormatter.AppendLine(sb, 0, $"export const {schemaName}: z.ZodType<{typeName}> = z.object({{");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 0, $"export const {schemaName} = z.object({{");
        }
        
        foreach (var property in data.Properties)
        {
            GeneratePropertySchema(sb, property, indentLevel: 1);
        }
        
        CodeFormatter.AppendLine(sb, 0, "});");
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Zod схему для enum
    /// </summary>
    public string RenderEnum(EnumTemplateData data)
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

    #region Private Helper Methods

    private void GeneratePropertyType(StringBuilder sb, IPropertyTemplateData property, int indentLevel)
    {
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, indentLevel, property.Description);
        
        // Генерируем тип свойства
        var optional = property.IsOptional ? "?" : "";
        var typeString = property.TypeInfo.FullTypeName;
        CodeFormatter.AppendLine(sb, indentLevel, $"{property.Name}{optional}: {typeString};");
    }

    private void GeneratePropertySchema(StringBuilder sb, IPropertyTemplateData property, int indentLevel)
    {
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, indentLevel, property.Description);
        
        // Генерируем Zod схему для свойства
        // Получаем Zod схему из Options через приведение через object
        string zodSchema;
        try
        {
            var typedProperty = (PropertyTemplateData<ZodPropertyTemplateDataOptions>)(object)property;
            zodSchema = typedProperty.Options.ZodSchema ?? "z.any()";
        }
        catch
        {
            zodSchema = "z.any()";
        }
        var optional = property.IsRequired ? "" : ".optional()";
        
        CodeFormatter.AppendLine(sb, indentLevel, $"{property.Name}: {zodSchema}{optional},");
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

    private string SchemaNameToFileName(string schemaName)
    {
        // Удаляем суффикс "Schema" только с конца строки
        var typeName = schemaName.EndsWith("Schema", StringComparison.OrdinalIgnoreCase) 
            ? schemaName.Substring(0, schemaName.Length - 6) 
            : schemaName;
            
        if (string.IsNullOrEmpty(typeName))
            return schemaName.ToLowerInvariant();
        
        return _namingConverter.ToCamelCase(typeName);
    }

    private string TypeNameToFileName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return typeName.ToLowerInvariant();
        
        return _namingConverter.ToCamelCase(typeName);
    }




    #endregion
}
