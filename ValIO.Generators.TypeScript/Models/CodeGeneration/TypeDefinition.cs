namespace ValIO.Generators.TypeScript.Models.CodeGeneration;

/// <summary>
/// Представляет определение типа TypeScript
/// </summary>
public sealed class TypeDefinition
{
    /// <summary>
    /// Имя типа
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Тип (например, "string", "number", "User", "Array<User>")
    /// </summary>
    public required string Type { get; init; }
    
    /// <summary>
    /// Является ли тип опциональным
    /// </summary>
    public bool IsOptional { get; init; }
    
    /// <summary>
    /// Описание типа (для JSDoc)
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Форматирует определение типа в строку TypeScript
    /// </summary>
    public string Format(int indentLevel = 0)
    {
        var indent = new string(' ', indentLevel * 4);
        var optional = IsOptional ? "?" : "";
        return $"{indent}{Name}{optional}: {Type};";
    }
}

/// <summary>
/// Представляет объект типа (для variables в мутациях)
/// </summary>
public sealed class TypeObjectDefinition
{
    /// <summary>
    /// Список свойств типа
    /// </summary>
    public required IReadOnlyList<TypeDefinition> Properties { get; init; }
    
    /// <summary>
    /// Форматирует объект типа в строку TypeScript
    /// </summary>
    public string Format(int indentLevel = 0)
    {
        if (Properties.Count == 0)
            return "{}";
        
        var indent = new string(' ', indentLevel * 4);
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("{");
        
        foreach (var property in Properties)
        {
            sb.AppendLine(property.Format(indentLevel + 1));
        }
        
        sb.Append($"{indent}}}");
        
        return sb.ToString();
    }
}
