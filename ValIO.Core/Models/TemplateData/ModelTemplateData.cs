using ValIO.Core.Interfaces;
using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Core.Models.TemplateData;

/// <summary>
/// Универсальные данные для шаблона модели (класс, интерфейс, тип, схема валидации)
/// </summary>
/// <typeparam name="TOptions">Тип опций модели (язык-специфичный)</typeparam>
/// <typeparam name="TPropertyOptions">Тип опций свойств (язык-специфичный)</typeparam>
public class ModelTemplateData<TOptions, TPropertyOptions> : IModelTemplateData
    where TOptions : ModelTemplateDataOptions, new()
    where TPropertyOptions : PropertyTemplateDataOptions, new()
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
    public IReadOnlyList<PropertyTemplateData<TPropertyOptions>> Properties { get; set; } = Array.Empty<PropertyTemplateData<TPropertyOptions>>();
    
    /// <summary>
    /// Свойства модели для интерфейса IModelTemplateData
    /// </summary>
    IReadOnlyList<IPropertyTemplateData> IModelTemplateData.Properties => Properties.Cast<IPropertyTemplateData>().ToList();
    
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
    public IReadOnlyList<PropertyTemplateData<TPropertyOptions>> BaseClassProperties { get; set; } = Array.Empty<PropertyTemplateData<TPropertyOptions>>();
    
    /// <summary>
    /// Свойства базового класса для интерфейса IModelTemplateData
    /// </summary>
    IReadOnlyList<IPropertyTemplateData> IModelTemplateData.BaseClassProperties => BaseClassProperties.Cast<IPropertyTemplateData>().ToList();
    
    /// <summary>
    /// Собственные свойства модели (не из базового класса)
    /// </summary>
    public IReadOnlyList<PropertyTemplateData<TPropertyOptions>> OwnProperties { get; set; } = Array.Empty<PropertyTemplateData<TPropertyOptions>>();
    
    /// <summary>
    /// Собственные свойства для интерфейса IModelTemplateData
    /// </summary>
    IReadOnlyList<IPropertyTemplateData> IModelTemplateData.OwnProperties => OwnProperties.Cast<IPropertyTemplateData>().ToList();
    
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
    public TOptions Options { get; set; } = new();
}

/// <summary>
/// Универсальные данные для шаблона модели (класс, интерфейс, тип, схема валидации)
/// Безтипная версия для обратной совместимости
/// </summary>
public class ModelTemplateData : ModelTemplateData<ModelTemplateDataOptions, PropertyTemplateDataOptions>
{
}

/// <summary>
/// Универсальные данные для шаблона свойства
/// </summary>
/// <typeparam name="TOptions">Тип опций (язык-специфичный)</typeparam>
public class PropertyTemplateData<TOptions> : IPropertyTemplateData where TOptions : PropertyTemplateDataOptions, new()
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
    public TOptions Options { get; set; } = new();
}

/// <summary>
/// Универсальные данные для шаблона свойства
/// Безтипная версия для обратной совместимости
/// </summary>
public class PropertyTemplateData : PropertyTemplateData<PropertyTemplateDataOptions>
{
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
