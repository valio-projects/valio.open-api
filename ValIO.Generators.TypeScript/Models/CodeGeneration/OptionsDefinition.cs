namespace ValIO.Generators.TypeScript.Models.CodeGeneration;

/// <summary>
/// Представляет определение options для функций
/// </summary>
public sealed class OptionsDefinition
{
    /// <summary>
    /// Список свойств options
    /// </summary>
    public required IReadOnlyList<OptionsProperty> Properties { get; init; }
    
    /// <summary>
    /// Является ли options опциональным
    /// </summary>
    public bool IsOptional { get; init; } = true;
    
    /// <summary>
    /// Форматирует options в строку TypeScript
    /// </summary>
    public string Format(int indentLevel = 0)
    {
        var indent = new string(' ', indentLevel * 4);
        var optional = IsOptional ? "?" : "";
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"{indent}options{optional}: {{");
        
        foreach (var property in Properties)
        {
            sb.AppendLine(property.Format(indentLevel + 1));
        }
        
        sb.Append($"{indent}}}");
        
        return sb.ToString();
    }
}

/// <summary>
/// Представляет свойство в options
/// </summary>
public sealed class OptionsProperty
{
    /// <summary>
    /// Имя свойства
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Тип свойства
    /// </summary>
    public required string Type { get; init; }
    
    /// <summary>
    /// Является ли свойство опциональным
    /// </summary>
    public bool IsOptional { get; init; }
    
    /// <summary>
    /// Описание свойства (для JSDoc)
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Форматирует свойство в строку TypeScript
    /// </summary>
    public string Format(int indentLevel = 0)
    {
        var indent = new string(' ', indentLevel * 4);
        var optional = IsOptional ? "?" : "";
        return $"{indent}{Name}{optional}: {Type};";
    }
}
