using ValIO.Core.Interfaces;

namespace ValIO.Generators.TypeScript.Zod.Models;

/// <summary>
/// Данные для шаблона Zod схемы модели
/// </summary>
public class ZodSchemaTemplateData
{
    public string SchemaName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<ZodPropertySchemaTemplateData> Properties { get; set; } = Array.Empty<ZodPropertySchemaTemplateData>();
    
    /// <summary>
    /// Список зависимостей - схем, которые нужно импортировать
    /// </summary>
    public IReadOnlyList<string> SchemaDependencies { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Список зависимостей - enum схем, которые нужно импортировать
    /// </summary>
    public IReadOnlyList<string> EnumDependencies { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Путь к папке с моделями для импортов (относительный путь)
    /// </summary>
    public string? ModelsPath { get; set; }
    
    /// <summary>
    /// Конвертер имен для преобразования имен схем в имена файлов
    /// </summary>
    public INamingConverter? NamingConverter { get; set; }
    
    /// <summary>
    /// Флаг, указывающий, что схема содержит циклическую ссылку на саму себя
    /// </summary>
    public bool HasCircularReference { get; set; }
    
    /// <summary>
    /// Функция для преобразования имени схемы в имя файла (camelCase)
    /// </summary>
    public string SchemaNameToFileName(string schemaName)
    {
        // Удаляем суффикс "Schema" только с конца строки, чтобы не удалять "Schema" из середины имени
        // Например, "LayoutSchemaGroupModelSchema" должно стать "LayoutSchemaGroupModel", а не "LayoutGroupModel"
        var typeName = schemaName.EndsWith("Schema", StringComparison.OrdinalIgnoreCase) 
            ? schemaName.Substring(0, schemaName.Length - 6) 
            : schemaName;
            
        if (string.IsNullOrEmpty(typeName))
            return schemaName.ToLowerInvariant();
        
        // Используем NamingConverter, если он доступен
        if (NamingConverter != null)
        {
            return NamingConverter.ToCamelCase(typeName);
        }
        
        // Fallback: правильное преобразование в camelCase
        var words = SplitIntoWords(typeName);
        if (words.Count == 0)
            return typeName.ToLowerInvariant();
        
        var firstWord = words[0].ToLowerInvariant();
        var rest = words.Skip(1).Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant());
        return firstWord + string.Join("", rest);
    }
    
    private static List<string> SplitIntoWords(string input)
    {
        var words = new List<string>();
        var currentWord = new System.Text.StringBuilder();

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
}

/// <summary>
/// Данные для шаблона Zod схемы свойства
/// </summary>
public class ZodPropertySchemaTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string ZodSchema { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
    
    /// <summary>
    /// TypeScript тип свойства (для генерации интерфейсов)
    /// </summary>
    public string? TypeScriptType { get; set; }
}

/// <summary>
/// Данные для шаблона Zod схемы enum
/// </summary>
public class ZodEnumSchemaTemplateData
{
    public string SchemaName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<string> EnumValues { get; set; } = Array.Empty<string>();
}
