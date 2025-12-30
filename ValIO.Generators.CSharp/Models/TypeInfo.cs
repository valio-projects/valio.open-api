namespace ValIO.Generators.CSharp.Models;

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
    /// Является ли тип Task или Task<T>
    /// </summary>
    public bool IsTask { get; set; }

    /// <summary>
    /// Является ли тип nullable
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Является ли тип файлом (Stream, byte[])
    /// </summary>
    public bool IsFile { get; set; }

    /// <summary>
    /// Является ли тип примитивным (int, string, bool и т.д.)
    /// </summary>
    public bool IsPrimitive { get; set; }

    /// <summary>
    /// Является ли тип void
    /// </summary>
    public bool IsVoid { get; set; }

    /// <summary>
    /// Является ли тип HttpResponseMessage
    /// </summary>
    public bool IsHttpResponseMessage { get; set; }

    /// <summary>
    /// Аргументы типа для generic типов (например, для List<T> - [T], для Dictionary<K,V> - [K, V])
    /// </summary>
    public IReadOnlyList<TypeInfo> GenericArguments { get; set; } = Array.Empty<TypeInfo>();

    /// <summary>
    /// Полное представление типа как строки (для обратной совместимости и отладки)
    /// </summary>
    public string FullTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Создает простой тип (не generic, не nullable)
    /// </summary>
    public static TypeInfo Simple(string baseType, bool isPrimitive = false, bool isFile = false)
    {
        return new TypeInfo
        {
            BaseType = baseType,
            IsPrimitive = isPrimitive,
            IsFile = isFile,
            FullTypeName = baseType
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
            IsTask = innerType.IsTask,
            IsNullable = true,
            IsFile = innerType.IsFile,
            IsPrimitive = innerType.IsPrimitive,
            IsVoid = innerType.IsVoid,
            IsHttpResponseMessage = innerType.IsHttpResponseMessage,
            GenericArguments = innerType.GenericArguments,
            FullTypeName = $"{innerType.FullTypeName}?"
        };
    }

    /// <summary>
    /// Создает коллекцию (List<T>, Array<T> и т.д.)
    /// </summary>
    public static TypeInfo Collection(string collectionType, TypeInfo itemType, bool isNullable = false)
    {
        var fullTypeName = $"{collectionType}<{itemType.FullTypeName}>";
        if (isNullable)
        {
            fullTypeName += "?";
        }

        return new TypeInfo
        {
            BaseType = collectionType,
            IsCollection = true,
            IsNullable = isNullable,
            GenericArguments = new[] { itemType },
            FullTypeName = fullTypeName
        };
    }

    /// <summary>
    /// Создает словарь (Dictionary<K,V>)
    /// </summary>
    public static TypeInfo Dictionary(string dictionaryType, TypeInfo keyType, TypeInfo valueType, bool isNullable = false)
    {
        var fullTypeName = $"{dictionaryType}<{keyType.FullTypeName}, {valueType.FullTypeName}>";
        if (isNullable)
        {
            fullTypeName += "?";
        }

        return new TypeInfo
        {
            BaseType = dictionaryType,
            IsDictionary = true,
            IsNullable = isNullable,
            GenericArguments = new[] { keyType, valueType },
            FullTypeName = fullTypeName
        };
    }

    /// <summary>
    /// Создает Task или Task<T>
    /// </summary>
    public static TypeInfo Task(TypeInfo? resultType = null, bool isNullable = false)
    {
        if (resultType == null)
        {
            return new TypeInfo
            {
                BaseType = "Task",
                IsTask = true,
                IsNullable = isNullable,
                FullTypeName = "Task"
            };
        }

        var fullTypeName = $"Task<{resultType.FullTypeName}>";
        if (isNullable)
        {
            fullTypeName += "?";
        }

        return new TypeInfo
        {
            BaseType = "Task",
            IsTask = true,
            IsNullable = isNullable,
            GenericArguments = new[] { resultType },
            FullTypeName = fullTypeName
        };
    }

    /// <summary>
    /// Создает void тип
    /// </summary>
    public static TypeInfo Void()
    {
        return new TypeInfo
        {
            BaseType = "void",
            IsVoid = true,
            FullTypeName = "void"
        };
    }

    /// <summary>
    /// Создает HttpResponseMessage тип
    /// </summary>
    public static TypeInfo HttpResponseMessage(bool isNullable = false)
    {
        return new TypeInfo
        {
            BaseType = "HttpResponseMessage",
            IsHttpResponseMessage = true,
            IsNullable = isNullable,
            FullTypeName = isNullable ? "HttpResponseMessage?" : "HttpResponseMessage"
        };
    }
}
