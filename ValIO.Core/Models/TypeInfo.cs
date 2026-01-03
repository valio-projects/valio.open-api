using ValIO.Core.Models;

namespace ValIO.Core.Models;

/// <summary>
/// Информация о типе с мета-данными для генерации кода
/// </summary>
public class TypeInfo
{
    /// <summary>
    /// Базовое имя типа (например, "string", "int", "User", "List")
    /// </summary>
    public string BaseType { get; set; } = string.Empty;

    /// <summary>
    /// Является ли тип коллекцией (List, Array, IEnumerable и т.д.)
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    /// Является ли тип словарем (Dictionary, IDictionary и т.д.)
    /// </summary>
    public bool IsDictionary { get; set; }

    /// <summary>
    /// Является ли тип nullable
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Является ли тип опциональным (для TypeScript - optional property)
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Является ли тип файлом (Stream, byte[], Blob)
    /// </summary>
    public bool IsFile { get; set; }

    /// <summary>
    /// Является ли тип примитивным (int, string, bool и т.д.)
    /// </summary>
    public bool IsPrimitive { get; set; }


    /// <summary>
    /// Аргументы типа для generic типов (например, для List<T> - [T], для Dictionary<K,V> - [K, V])
    /// </summary>
    public IReadOnlyList<TypeInfo> GenericArguments { get; set; } = Array.Empty<TypeInfo>();

    /// <summary>
    /// Полное представление типа как строки (для обратной совместимости и отладки)
    /// </summary>
    public string FullTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Язык генерации (для определения синтаксиса)
    /// </summary>
    public GenerationLanguage? Language { get; set; }

    /// <summary>
    /// Список зависимостей (имена типов, которые нужно импортировать)
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Формат типа (enum, union, intersection, object)
    /// </summary>
    public TypeFormat Format { get; set; } = TypeFormat.Object;

    /// <summary>
    /// Определяет, является ли тип value type (структура, примитив, enum)
    /// </summary>
    public bool IsValueType()
    {
        // Примитивные типы (кроме string) - это value types
        if (IsPrimitive && BaseType != "string" && BaseType != "object")
        {
            return true;
        }

        // Известные value types
        var valueTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bool", "byte", "sbyte", "char", "decimal", "double", "float",
            "int", "uint", "long", "ulong", "short", "ushort",
            "Guid", "DateTime", "DateTimeOffset", "TimeSpan"
        };

        // Проверяем базовый тип
        if (valueTypes.Contains(BaseType))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Определяет, является ли тип non-nullable value type (не может быть null)
    /// </summary>
    public bool IsNonNullableValueType()
    {
        return IsValueType() && !IsNullable;
    }

    /// <summary>
    /// Создает простой тип (не generic, не nullable)
    /// </summary>
    public static TypeInfo Simple(string baseType, bool isPrimitive = false, bool isFile = false, GenerationLanguage? language = null)
    {
        return new TypeInfo
        {
            BaseType = baseType,
            IsPrimitive = isPrimitive,
            IsFile = isFile,
            FullTypeName = baseType,
            Language = language,
            Format = TypeFormat.Object
        };
    }

    /// <summary>
    /// Создает nullable тип
    /// </summary>
    public static TypeInfo Nullable(TypeInfo innerType)
    {
        return new TypeInfo
        {
            BaseType = innerType.BaseType,
            IsCollection = innerType.IsCollection,
            IsDictionary = innerType.IsDictionary,
            IsNullable = true,
            IsOptional = innerType.IsOptional,
            IsFile = innerType.IsFile,
            IsPrimitive = innerType.IsPrimitive,
            GenericArguments = innerType.GenericArguments,
            // FullTypeName НЕ должен содержать "?" - nullable добавляется в рендерере на основе IsNullable
            FullTypeName = innerType.FullTypeName,
            Language = innerType.Language,
            Dependencies = innerType.Dependencies,
            Format = innerType.Format
        };
    }

    /// <summary>
    /// Создает коллекцию (List<T>, Array<T> и т.д.)
    /// </summary>
    public static TypeInfo Collection(string collectionType, TypeInfo itemType, bool isNullable = false, GenerationLanguage? language = null)
    {
        // FullTypeName НЕ должен содержать "?" - nullable добавляется в рендерере на основе IsNullable
        var fullTypeName = language switch
        {
            GenerationLanguage.TypeScript => $"{itemType.FullTypeName}[]",
            _ => $"{collectionType}<{itemType.FullTypeName}>"
        };

        return new TypeInfo
        {
            BaseType = collectionType,
            IsCollection = true,
            IsNullable = isNullable,
            IsOptional = isNullable && language == GenerationLanguage.TypeScript,
            GenericArguments = new[] { itemType },
            FullTypeName = fullTypeName,
            Language = language ?? itemType.Language,
            Dependencies = itemType.Dependencies,
            Format = TypeFormat.Object
        };
    }

    /// <summary>
    /// Создает словарь (Dictionary<K,V>)
    /// </summary>
    public static TypeInfo Dictionary(string dictionaryType, TypeInfo keyType, TypeInfo valueType, bool isNullable = false, GenerationLanguage? language = null)
    {
        // FullTypeName НЕ должен содержать "?" - nullable добавляется в рендерере на основе IsNullable
        var fullTypeName = language switch
        {
            GenerationLanguage.TypeScript => $"Record<{keyType.FullTypeName}, {valueType.FullTypeName}>",
            _ => $"{dictionaryType}<{keyType.FullTypeName}, {valueType.FullTypeName}>"
        };

        return new TypeInfo
        {
            BaseType = dictionaryType,
            IsDictionary = true,
            IsNullable = isNullable,
            IsOptional = isNullable && language == GenerationLanguage.TypeScript,
            GenericArguments = new[] { keyType, valueType },
            FullTypeName = fullTypeName,
            Language = language ?? keyType.Language ?? valueType.Language,
            Dependencies = keyType.Dependencies.Concat(valueType.Dependencies).Distinct().ToList(),
            Format = TypeFormat.Object
        };
    }

    /// <summary>
    /// Создает void тип
    /// </summary>
    public static TypeInfo Void(GenerationLanguage? language = null)
    {
        return new TypeInfo
        {
            BaseType = "void",
            FullTypeName = "void",
            Language = language,
            Format = TypeFormat.Object
        };
    }
}

/// <summary>
/// Формат типа для генерации
/// </summary>
public enum TypeFormat
{
    /// <summary>
    /// Обычный объект/класс/интерфейс
    /// </summary>
    Object,
    
    /// <summary>
    /// Enum
    /// </summary>
    Enum,
    
    /// <summary>
    /// Union type (TypeScript: A | B)
    /// </summary>
    Union,
    
    /// <summary>
    /// Intersection type (TypeScript: A & B)
    /// </summary>
    Intersection
}
