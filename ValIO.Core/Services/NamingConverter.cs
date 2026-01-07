using System.Text;
using System.Text.RegularExpressions;
using ValIO.Core.Interfaces;

namespace ValIO.Core.Services;

/// <summary>
/// Реализация конвертера имен
/// </summary>
public class NamingConverter : INamingConverter
{
    public string ToPascalCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var words = SplitIntoWords(name);
        return string.Join("", words.Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
    }

    public string ToCamelCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var words = SplitIntoWords(name);
        var firstWord = words[0].ToLowerInvariant();
        var rest = words.Skip(1).Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant());
        return firstWord + string.Join("", rest);
    }

    public string ToSnakeCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var words = SplitIntoWords(name);
        return string.Join("_", words.Select(w => w.ToLowerInvariant()));
    }

    public string ToKebabCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var words = SplitIntoWords(name);
        return string.Join("-", words.Select(w => w.ToLowerInvariant()));
    }

    public string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "_";

        // Удаляем недопустимые символы и заменяем на подчеркивание
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
        
        // Если начинается с цифры, добавляем префикс
        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;

        // Если пустая строка после санитизации
        if (string.IsNullOrWhiteSpace(sanitized))
            return "_";

        return sanitized;
    }

    private static List<string> SplitIntoWords(string input)
    {
        var words = new List<string>();
        var currentWord = new StringBuilder();

        foreach (var c in input)
        {
            if (char.IsUpper(c) && currentWord.Length > 0)
            {
                words.Add(currentWord.ToString());
                currentWord.Clear();
            }
            else if (c == '_' || c == '-' || c == ' ' || c == '.')
            {
                if (currentWord.Length > 0)
                {
                    words.Add(currentWord.ToString());
                    currentWord.Clear();
                }
                continue;
            }

            if (char.IsLetterOrDigit(c))
                currentWord.Append(c);
        }

        if (currentWord.Length > 0)
            words.Add(currentWord.ToString());

        return words.Count > 0 ? words : new List<string> { input };
    }
    
    /// <summary>
    /// По умолчанию не обрабатывает зарезервированные ключевые слова (базовая реализация)
    /// Конкретные языковые реализации должны переопределить этот метод
    /// </summary>
    public virtual string SanitizeReservedKeyword(string name)
    {
        // Базовая реализация - просто возвращает имя как есть
        // Языково-специфичные реализации должны переопределить этот метод
        return name;
    }
    
    /// <summary>
    /// По умолчанию не проверяет зарезервированные ключевые слова (базовая реализация)
    /// Конкретные языковые реализации должны переопределить этот метод
    /// </summary>
    public virtual bool IsReservedKeyword(string name)
    {
        // Базовая реализация - всегда возвращает false
        // Языково-специфичные реализации должны переопределить этот метод
        return false;
    }
}

