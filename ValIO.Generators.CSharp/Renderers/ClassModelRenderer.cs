using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;

namespace ValIO.Generators.CSharp.Renderers;

/// <summary>
/// Рендерер для генерации C# классов
/// </summary>
public class ClassModelRenderer : IModelFormatRenderer
{
    public ModelFormat Format => ModelFormat.Class;
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
        CommentGenerator.GenerateXmlComment(sb, 0, data.Description);
        
        // Генерируем класс
        var classDeclaration = new StringBuilder();
        classDeclaration.Append("public class ");
        classDeclaration.Append(data.Name);
        
        // Добавляем базовый класс, если есть
        if (!string.IsNullOrEmpty(data.BaseClassName))
        {
            classDeclaration.Append($" : {data.BaseClassName}");
        }
        
        sb.AppendLine(classDeclaration.ToString());
        sb.AppendLine("{");
        
        // Генерируем свойства
        foreach (var propertyBase in data.Properties)
        {
            // Приводим типизированное свойство к базовому типу
            var property = propertyBase as PropertyTemplateData ?? (PropertyTemplateData)(object)propertyBase;
            GenerateProperty(sb, property, indentLevel: 1);
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    private void GenerateProperty(StringBuilder sb, PropertyTemplateData property, int indentLevel)
    {
        // Генерируем XML комментарий
        CommentGenerator.GenerateXmlComment(sb, indentLevel, property.Description);
        
        // Генерируем свойство
        var nullable = property.IsNullable ? "?" : "";
        var typeString = property.TypeInfo.FullTypeName;
        var defaultValue = property.DefaultValue != null ? $" = {DefaultValueFormatter.FormatDefaultValue(property.DefaultValue, property.TypeInfo)}" : "";
        
        CodeFormatter.AppendLine(sb, indentLevel, $"public {typeString}{nullable} {property.Name} {{ get; set; }}{defaultValue}");
    }

    // Методы FormatDefaultValue и EscapeXmlComment теперь в ValIO.Core.Utilities

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
