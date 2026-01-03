namespace ValIO.Core.Models.TemplateData;

/// <summary>
/// Универсальные данные для шаблона модели (класс, интерфейс, тип, схема валидации)
/// </summary>
public class ModelTemplateData
{
    /// <summary>
    /// Имя модели (класс, интерфейс, тип)
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Описание модели
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Свойства модели
    /// </summary>
    public IReadOnlyList<PropertyTemplateData> Properties { get; set; } = Array.Empty<PropertyTemplateData>();
    
    /// <summary>
    /// Язык генерации
    /// </summary>
    public GenerationLanguage Language { get; set; }
    
    /// <summary>
    /// Формат модели (class, interface, type, schema)
    /// </summary>
    public ModelFormat Format { get; set; } = ModelFormat.Class;
    
    /// <summary>
    /// Список зависимостей (имена типов, которые нужно импортировать)
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Имя базового класса/интерфейса (для allOf - наследование)
    /// </summary>
    public string? BaseClassName { get; set; }
    
    /// <summary>
    /// Свойства, которые принадлежат базовому классу (из allOf)
    /// </summary>
    public IReadOnlyList<PropertyTemplateData> BaseClassProperties { get; set; } = Array.Empty<PropertyTemplateData>();
    
    /// <summary>
    /// Собственные свойства модели (не из базового класса)
    /// </summary>
    public IReadOnlyList<PropertyTemplateData> OwnProperties { get; set; } = Array.Empty<PropertyTemplateData>();
    
    /// <summary>
    /// Namespace (для C#)
    /// </summary>
    public string? Namespace { get; set; }
    
    /// <summary>
    /// Путь к файлу для импорта (для TypeScript)
    /// </summary>
    public string? ImportPath { get; set; }
    
    /// <summary>
    /// Дополнительные опции для генерации (язык-специфичные)
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();
}

/// <summary>
/// Универсальные данные для шаблона свойства
/// </summary>
public class PropertyTemplateData
{
    /// <summary>
    /// Имя свойства
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о типе свойства с мета-данными
    /// </summary>
    public TypeInfo TypeInfo { get; set; } = null!;
    
    /// <summary>
    /// Описание свойства
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Является ли свойство обязательным
    /// </summary>
    public bool IsRequired { get; set; } = false;
    
    /// <summary>
    /// Является ли свойство nullable (для C# - добавляется "?")
    /// </summary>
    public bool IsNullable { get; set; } = false;
    
    /// <summary>
    /// Является ли свойство опциональным (для TypeScript - добавляется "?")
    /// </summary>
    public bool IsOptional { get; set; } = false;
    
    /// <summary>
    /// Значение по умолчанию
    /// </summary>
    public object? DefaultValue { get; set; }
    
    /// <summary>
    /// Минимальное значение (для чисел)
    /// </summary>
    public decimal? Min { get; set; }
    
    /// <summary>
    /// Максимальное значение (для чисел)
    /// </summary>
    public decimal? Max { get; set; }
    
    /// <summary>
    /// Минимальная длина (для строк)
    /// </summary>
    public int? MinLength { get; set; }
    
    /// <summary>
    /// Максимальная длина (для строк)
    /// </summary>
    public int? MaxLength { get; set; }
    
    /// <summary>
    /// Исключающее минимальное значение (для чисел, > вместо >=)
    /// </summary>
    public decimal? ExclusiveMin { get; set; }
    
    /// <summary>
    /// Исключающее максимальное значение (для чисел, < вместо <=)
    /// </summary>
    public decimal? ExclusiveMax { get; set; }
    
    /// <summary>
    /// Регулярное выражение для валидации (для строк)
    /// </summary>
    public string? Pattern { get; set; }
    
    /// <summary>
    /// Дополнительные опции для генерации (язык-специфичные)
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();
}

/// <summary>
/// Формат модели для генерации
/// </summary>
public enum ModelFormat
{
    /// <summary>
    /// Класс (C#)
    /// </summary>
    Class,
    
    /// <summary>
    /// Record (C#)
    /// </summary>
    Record,
    
    /// <summary>
    /// Struct (C#)
    /// </summary>
    Struct,
    
    /// <summary>
    /// Интерфейс (TypeScript)
    /// </summary>
    Interface,
    
    /// <summary>
    /// Type alias (TypeScript)
    /// </summary>
    Type,
    
    /// <summary>
    /// Схема валидации (Zod, и т.д.)
    /// </summary>
    ValidationSchema
}
