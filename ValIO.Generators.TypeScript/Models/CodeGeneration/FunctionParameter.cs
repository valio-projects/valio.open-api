namespace ValIO.Generators.TypeScript.Models.CodeGeneration;

/// <summary>
/// Представляет параметр функции TypeScript
/// </summary>
public sealed class FunctionParameter
{
    /// <summary>
    /// Имя параметра
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Тип параметра
    /// </summary>
    public required string Type { get; init; }
    
    /// <summary>
    /// Является ли параметр опциональным
    /// </summary>
    public bool IsOptional { get; init; }
    
    /// <summary>
    /// Описание параметра (для JSDoc)
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Значение по умолчанию (если есть)
    /// </summary>
    public string? DefaultValue { get; init; }
    
    /// <summary>
    /// Форматирует параметр в строку TypeScript
    /// </summary>
    public string Format()
    {
        var optional = IsOptional ? "?" : "";
        var defaultValue = DefaultValue != null ? $" = {DefaultValue}" : "";
        return $"{Name}{optional}: {Type}{defaultValue}";
    }
    
    /// <summary>
    /// Создает параметр для options с многострочным типом
    /// </summary>
    public static FunctionParameter CreateOptionsParameter(OptionsDefinition optionsDefinition)
    {
        // Форматируем options как многострочный тип
        // Извлекаем только свойства из optionsDefinition, без внешних скобок
        var typeLines = new List<string>();
        foreach (var prop in optionsDefinition.Properties)
        {
            typeLines.Add(prop.Format(1).TrimStart());
        }
        
        return new FunctionParameter
        {
            Name = "options",
            Type = string.Join("\n", typeLines),
            IsOptional = optionsDefinition.IsOptional
        };
    }
}
