using System.Text;

namespace ValIO.Core.Utilities;

/// <summary>
/// Утилита для форматирования кода с поддержкой отступов
/// </summary>
public static class CodeFormatter
{
    private const string Tab = "    "; // 4 пробела для одного уровня отступа
    
    /// <summary>
    /// Добавляет строку с отступом в StringBuilder
    /// </summary>
    /// <param name="sb">StringBuilder для добавления строки</param>
    /// <param name="indentLevel">Уровень отступа (количество табов)</param>
    /// <param name="line">Строка для добавления</param>
    public static void AppendLine(StringBuilder sb, int indentLevel, string line)
    {
        if (sb == null)
            throw new ArgumentNullException(nameof(sb));
        
        if (indentLevel < 0)
            indentLevel = 0;
        
        var indent = new string(' ', indentLevel * 4); // 4 пробела на уровень
        sb.AppendLine($"{indent}{line}");
    }
    
    /// <summary>
    /// Добавляет отступ в StringBuilder без новой строки
    /// </summary>
    /// <param name="sb">StringBuilder для добавления отступа</param>
    /// <param name="indentLevel">Уровень отступа (количество табов)</param>
    /// <param name="text">Текст для добавления после отступа</param>
    public static void Append(StringBuilder sb, int indentLevel, string text = "")
    {
        if (sb == null)
            throw new ArgumentNullException(nameof(sb));
        
        if (indentLevel < 0)
            indentLevel = 0;
        
        var indent = new string(' ', indentLevel * 4); // 4 пробела на уровень
        sb.Append($"{indent}{text}");
    }
    
    /// <summary>
    /// Добавляет пустую строку в StringBuilder
    /// </summary>
    public static void AppendEmptyLine(StringBuilder sb)
    {
        if (sb == null)
            throw new ArgumentNullException(nameof(sb));
        
        sb.AppendLine();
    }
    
    /// <summary>
    /// Добавляет несколько строк с одинаковым отступом
    /// </summary>
    public static void AppendLines(StringBuilder sb, int indentLevel, params string[] lines)
    {
        if (sb == null)
            throw new ArgumentNullException(nameof(sb));
        
        foreach (var line in lines)
        {
            AppendLine(sb, indentLevel, line);
        }
    }
}
