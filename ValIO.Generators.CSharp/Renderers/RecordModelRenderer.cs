using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Generators.CSharp.Renderers;

/// <summary>
/// Рендерер для генерации C# records
/// </summary>
public class RecordModelRenderer : IModelFormatRenderer
{
    public ModelFormat Format => ModelFormat.Record;
    public GenerationLanguage Language => GenerationLanguage.CSharp;

    public string Render(ModelTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Генерируем using директивы
        if (data.Dependencies.Count > 0)
        {
            var namespaces = ExtractNamespaces(data.Dependencies);
            foreach (var ns in namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"using {ns};");
            }
            sb.AppendLine();
        }
        
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
        
        // Генерируем record
        var recordDeclaration = new StringBuilder();
        recordDeclaration.Append("public record ");
        recordDeclaration.Append(data.Name);
        
        // Добавляем базовый класс, если есть
        if (!string.IsNullOrEmpty(data.BaseClassName))
        {
            recordDeclaration.Append($" : {data.BaseClassName}");
        }
        
        sb.AppendLine(recordDeclaration.ToString());
        sb.AppendLine("{");
        
        // Генерируем свойства
        foreach (var property in data.Properties)
        {
            GenerateProperty(sb, property, indent: "    ");
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    private void GenerateProperty(StringBuilder sb, PropertyTemplateData property, string indent)
    {
        // Генерируем XML комментарий
        if (!string.IsNullOrEmpty(property.Description))
        {
            sb.AppendLine($"{indent}/// <summary>");
            sb.AppendLine($"{indent}/// {EscapeXmlComment(property.Description)}");
            sb.AppendLine($"{indent}/// </summary>");
        }
        
        // Генерируем свойство
        var nullable = property.IsNullable ? "?" : "";
        var typeString = FormatType(property.TypeInfo);
        var defaultValue = property.DefaultValue != null ? $" = {FormatDefaultValue(property.DefaultValue, property.TypeInfo)}" : "";
        
        sb.AppendLine($"{indent}public {typeString}{nullable} {property.Name} {{ get; init; }}{defaultValue}");
    }

    private string FormatType(TypeInfo typeInfo)
    {
        return typeInfo.FullTypeName;
    }

    private string FormatDefaultValue(object? value, TypeInfo typeInfo)
    {
        if (value == null)
            return "null";
        
        var baseType = typeInfo.BaseType;
        
        if (baseType == "string")
        {
            return $"\"{value}\"";
        }
        
        if (baseType == "bool")
        {
            return value.ToString()?.ToLowerInvariant() ?? "false";
        }
        
        if (baseType == "float")
        {
            return $"{value}f";
        }
        
        return value.ToString() ?? "null";
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

    private HashSet<string> ExtractNamespaces(IReadOnlyList<string> dependencies)
    {
        var namespaces = new HashSet<string>();
        
        // Простая эвристика: если тип содержит точку, это может быть namespace
        foreach (var dep in dependencies)
        {
            var lastDotIndex = dep.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                var ns = dep.Substring(0, lastDotIndex);
                namespaces.Add(ns);
            }
        }
        
        return namespaces;
    }
}
