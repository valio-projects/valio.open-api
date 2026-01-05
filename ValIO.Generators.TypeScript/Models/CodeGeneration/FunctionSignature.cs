namespace ValIO.Generators.TypeScript.Models.CodeGeneration;

/// <summary>
/// Представляет сигнатуру функции TypeScript
/// </summary>
public sealed class FunctionSignature
{
    /// <summary>
    /// Имя функции
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Generic параметры функции
    /// </summary>
    public IReadOnlyList<GenericParameter> GenericParameters { get; init; } = Array.Empty<GenericParameter>();
    
    /// <summary>
    /// Параметры функции
    /// </summary>
    public required IReadOnlyList<FunctionParameter> Parameters { get; init; }
    
    /// <summary>
    /// Возвращаемый тип
    /// </summary>
    public string? ReturnType { get; init; }
    
    /// <summary>
    /// Является ли функция async
    /// </summary>
    public bool IsAsync { get; init; }
    
    /// <summary>
    /// Является ли функция arrow function
    /// </summary>
    public bool IsArrowFunction { get; init; }
    
    /// <summary>
    /// Форматирует сигнатуру функции в строку TypeScript
    /// </summary>
    public string Format(int indentLevel = 0)
    {
        var indent = new string(' ', indentLevel * 4);
        var sb = new System.Text.StringBuilder();
        
        var asyncKeyword = IsAsync ? "async " : "";
        var exportKeyword = "export ";
        
        // Generic параметры
        var genericParams = GenericParameters.Count > 0
            ? $"<{string.Join(", ", GenericParameters.Select(g => g.Format()))}>"
            : "";
        
        // Параметры
        var paramsStr = Parameters.Count > 0
            ? string.Join(",\n" + new string(' ', (indentLevel + 1) * 4), Parameters.Select(p => p.Format()))
            : "";
        
        // Возвращаемый тип
        var returnTypeStr = ReturnType != null ? $": {ReturnType}" : "";
        
        if (IsArrowFunction)
        {
            sb.Append($"{indent}{exportKeyword}{asyncKeyword}const {Name}{genericParams} = ");
            if (Parameters.Count > 0)
            {
                sb.AppendLine("(");
                sb.AppendLine(string.Join(",\n", Parameters.Select(p => 
                    $"{new string(' ', (indentLevel + 1) * 4)}{p.Format()}")));
                sb.Append($"{indent})");
            }
            else
            {
                sb.Append("()");
            }
            sb.Append($"{returnTypeStr} =>");
        }
        else
        {
            sb.Append($"{indent}{exportKeyword}{asyncKeyword}function {Name}{genericParams}(");
            if (Parameters.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(string.Join(",\n", Parameters.Select(p => 
                    $"{new string(' ', (indentLevel + 1) * 4)}{p.Format()}")));
                sb.Append($"{indent})");
            }
            sb.Append($"{returnTypeStr} {{");
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Представляет generic параметр
/// </summary>
public sealed class GenericParameter
{
    /// <summary>
    /// Имя generic параметра
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Значение по умолчанию
    /// </summary>
    public string? DefaultValue { get; init; }
    
    /// <summary>
    /// Форматирует generic параметр в строку
    /// </summary>
    public string Format()
    {
        return DefaultValue != null ? $"{Name} = {DefaultValue}" : Name;
    }
}
