using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Generators.TypeScript.Renderers;

/// <summary>
/// Рендерер для генерации TypeScript интерфейсов
/// </summary>
public class InterfaceModelRenderer : IModelFormatRenderer
{
    public ModelFormat Format => ModelFormat.Interface;
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    public string Render(ModelTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Генерируем импорты
        if (data.Dependencies.Count > 0 && !string.IsNullOrEmpty(data.ImportPath))
        {
            foreach (var dependency in data.Dependencies.Distinct().OrderBy(d => d))
            {
                var importFileName = GetModelFileName(dependency);
                sb.AppendLine($"import type {{ {dependency} }} from '{data.ImportPath}/{importFileName}.model';");
            }
            sb.AppendLine();
        }
        
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(data.Description))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {EscapeJSDocComment(data.Description)}");
            sb.AppendLine(" */");
        }
        
        // Определяем формат модели
        var useTypeAlias = !string.IsNullOrEmpty(data.BaseClassName) && data.OwnProperties.Count == 0;
        var useIntersectionType = !string.IsNullOrEmpty(data.BaseClassName) && data.OwnProperties.Count > 0;
        
        if (useTypeAlias)
        {
            // Просто type alias
            sb.AppendLine($"export type {data.Name} = {data.BaseClassName};");
        }
        else if (useIntersectionType)
        {
            // Intersection type
            sb.AppendLine($"export type {data.Name} = {data.BaseClassName} & {{");
            foreach (var property in data.OwnProperties)
            {
                GenerateProperty(sb, property, indent: "  ");
            }
            sb.AppendLine("};");
        }
        else
        {
            // Обычный interface
            sb.AppendLine($"export interface {data.Name} {{");
            foreach (var property in data.Properties)
            {
                GenerateProperty(sb, property, indent: "  ");
            }
            sb.AppendLine("}");
        }
        
        return sb.ToString();
    }

    private void GenerateProperty(StringBuilder sb, PropertyTemplateData property, string indent)
    {
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(property.Description))
        {
            sb.AppendLine($"{indent}/**");
            sb.AppendLine($"{indent} * {EscapeJSDocComment(property.Description)}");
            sb.AppendLine($"{indent} */");
        }
        
        // Генерируем свойство
        var optional = property.IsOptional ? "?" : "";
        var typeString = FormatType(property.TypeInfo);
        sb.AppendLine($"{indent}{property.Name}{optional}: {typeString};");
    }

    private string FormatType(TypeInfo typeInfo)
    {
        return typeInfo.FullTypeName;
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

    private string GetModelFileName(string modelName)
    {
        // Преобразуем PascalCase в kebab-case для имени файла
        if (string.IsNullOrEmpty(modelName))
            return "model";
        
        var result = new StringBuilder();
        for (int i = 0; i < modelName.Length; i++)
        {
            var c = modelName[i];
            if (char.IsUpper(c) && i > 0)
            {
                result.Append('-');
            }
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }
}
