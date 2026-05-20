namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для конвертации имен (camelCase, PascalCase, etc.)
/// </summary>
public interface INamingConverter
{
    string ToPascalCase(string name);
    string ToCamelCase(string name);
    string ToSnakeCase(string name);
    string ToKebabCase(string name);
    string SanitizeIdentifier(string name);
    
    /// <summary>
    /// Обрабатывает зарезервированные ключевые слова, добавляя необходимые префиксы/суффиксы
    /// </summary>
    string SanitizeReservedKeyword(string name);
    
    /// <summary>
    /// Проверяет, является ли имя зарезервированным ключевым словом
    /// </summary>
    bool IsReservedKeyword(string name);
}

