namespace ValIO.Generators.TypeScript.Models.CodeGeneration;

/// <summary>
/// Представляет блок кода с отступами
/// </summary>
public sealed class CodeBlock
{
    /// <summary>
    /// Строки кода
    /// </summary>
    public required IReadOnlyList<CodeLine> Lines { get; init; }
    
    /// <summary>
    /// Базовый уровень отступа для всего блока
    /// </summary>
    public int BaseIndentLevel { get; init; }
    
    /// <summary>
    /// Форматирует блок кода в строку
    /// </summary>
    public string Format()
    {
        var sb = new System.Text.StringBuilder();
        
        foreach (var line in Lines)
        {
            var indent = new string(' ', (BaseIndentLevel + line.IndentLevel) * 4);
            sb.AppendLine($"{indent}{line.Content}");
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Представляет строку кода
/// </summary>
public sealed class CodeLine
{
    /// <summary>
    /// Содержимое строки
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// Уровень отступа относительно базового уровня блока
    /// </summary>
    public int IndentLevel { get; init; }
    
    /// <summary>
    /// Создает пустую строку
    /// </summary>
    public static CodeLine Empty => new() { Content = string.Empty, IndentLevel = 0 };
}
