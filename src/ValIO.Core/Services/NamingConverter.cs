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

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            // Explicit separators flush the current word.
            if (c == '_' || c == '-' || c == ' ' || c == '.')
            {
                if (currentWord.Length > 0)
                {
                    words.Add(currentWord.ToString());
                    currentWord.Clear();
                }
                continue;
            }

            if (!char.IsLetterOrDigit(c))
                continue;

            // Boundary heuristics for camel/Pascal/ALL_CAPS mixes:
            //   "myAPIClient" → ["my", "API", "Client"]
            //   "USER_ID"     → split by the underscore above; "USER" and "ID" stay intact
            //   "HTTPServer"  → ["HTTP", "Server"]
            // Two rules trigger a boundary BEFORE appending the current char:
            //   1. upper after lower:   lower→Upper  (e.g. "y" → "A")
            //   2. upper before lower:  run of uppers ending with one upper followed by a lower
            if (currentWord.Length > 0 && char.IsUpper(c))
            {
                var prev = currentWord[^1];
                var nextIsLower = i + 1 < input.Length && char.IsLower(input[i + 1]);
                var prevIsLowerOrDigit = char.IsLower(prev) || char.IsDigit(prev);
                var prevIsUpperAndNextIsLower = char.IsUpper(prev) && nextIsLower;

                if (prevIsLowerOrDigit || prevIsUpperAndNextIsLower)
                {
                    words.Add(currentWord.ToString());
                    currentWord.Clear();
                }
            }
            else if (currentWord.Length > 0 && char.IsDigit(c) && !char.IsDigit(currentWord[^1]))
            {
                // letter→digit also a boundary (e.g. "Version2" → ["Version", "2"])
                words.Add(currentWord.ToString());
                currentWord.Clear();
            }

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

