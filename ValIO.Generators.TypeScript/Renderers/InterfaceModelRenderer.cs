using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;

namespace ValIO.Generators.TypeScript.Renderers;

/// <summary>
/// Рендерер для генерации TypeScript интерфейсов
/// </summary>
public class InterfaceModelRenderer : IModelFormatRenderer
{
    public ModelFormat Format => ModelFormat.Interface;
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    public string Render(IModelTemplateData data)
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
        CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
        
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
                GenerateProperty(sb, property, indentLevel: 1);
            }
            CodeFormatter.AppendLine(sb, 0, "};");
        }
        else
        {
            // Обычный interface
            sb.AppendLine($"export interface {data.Name} {{");
            foreach (var property in data.Properties)
            {
                GenerateProperty(sb, property, indentLevel: 1);
            }
            CodeFormatter.AppendLine(sb, 0, "}");
        }
        
        return sb.ToString();
    }

    private void GenerateProperty(StringBuilder sb, IPropertyTemplateData property, int indentLevel)
    {
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, indentLevel, property.Description);
        
        // Генерируем свойство
        var optional = property.IsOptional ? "?" : "";
        var typeString = property.TypeInfo.FullTypeName;
        CodeFormatter.AppendLine(sb, indentLevel, $"{property.Name}{optional}: {typeString};");
    }

    // Метод EscapeJSDocComment теперь в ValIO.Core.Utilities.CodeEscapeHelper

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
