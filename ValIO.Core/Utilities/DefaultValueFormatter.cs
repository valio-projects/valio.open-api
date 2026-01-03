using ValIO.Core.Models;

namespace ValIO.Core.Utilities;

/// <summary>
/// Утилита для форматирования значений по умолчанию
/// </summary>
public static class DefaultValueFormatter
{
    /// <summary>
    /// Форматирует значение по умолчанию для C#
    /// </summary>
    public static string FormatDefaultValue(object? value, TypeInfo typeInfo)
    {
        if (value == null)
            return "null";
        
        var baseType = typeInfo.BaseType;
        
        return baseType switch
        {
            "string" => $"\"{CodeEscapeHelper.EscapeString(value.ToString())}\"",
            "bool" => value.ToString()?.ToLowerInvariant() ?? "false",
            "float" => $"{value}f",
            "double" => value.ToString() ?? "0.0",
            "decimal" => $"{value}m",
            "char" => $"'{CodeEscapeHelper.EscapeString(value.ToString())}'",
            _ => value.ToString() ?? "null"
        };
    }
    
    /// <summary>
    /// Форматирует значение по умолчанию для TypeScript
    /// </summary>
    public static string FormatTypeScriptDefaultValue(object? value, TypeInfo typeInfo)
    {
        if (value == null)
            return "null";
        
        var baseType = typeInfo.BaseType;
        
        return baseType switch
        {
            "string" => $"\"{CodeEscapeHelper.EscapeTypeScriptString(value.ToString())}\"",
            "boolean" => value.ToString()?.ToLowerInvariant() ?? "false",
            "number" => value.ToString() ?? "0",
            _ => value.ToString() ?? "null"
        };
    }
}
