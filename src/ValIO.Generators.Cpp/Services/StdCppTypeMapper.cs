using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.IR.Utilities;

namespace ValIO.Generators.Cpp.Services;

/// <summary>
/// Маппер IR типов в std-C++ TypeNode (std-профиль). Зеркало UETypeMapper по структуре
/// (включая SetEnumNamesForDocument), но цели: std::string/std::vector/std::optional
/// вместо FString/TArray; Nullable/Optional сохраняются как std::optional (в UE нельзя).
/// Union/Intersection маппятся в std::string с warning «raw JSON» (v1; backlog — typed variant).
/// </summary>
public sealed class StdCppTypeMapper
{
    private readonly StdNamingConvention _naming;
    private IReadOnlySet<string>? _enumNames;

    /// <summary>Предупреждения маппинга (union/intersection/any) — дренажируются шагом в context.Warnings.</summary>
    public ICollection<string> Warnings { get; } = new List<string>();

    public StdCppTypeMapper(StdNamingConvention naming)
    {
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
    }

    /// <summary>
    /// Устанавливает множество имён enum'ов текущего документа — отличает
    /// IrModelReference на enum от ссылки на структуру.
    /// </summary>
    public void SetEnumNamesForDocument(IReadOnlySet<string> enumNames)
    {
        _enumNames = enumNames;
    }

    /// <summary>
    /// Маппит IR тип в std-C++ TypeNode.
    /// </summary>
    public TypeNode MapType(IrType irType)
    {
        return irType switch
        {
            IrPrimitiveType primitive => MapPrimitiveType(primitive),
            IrModelReference modelRef => MapModelReference(modelRef),
            IrArrayType array => MapArrayType(array),
            IrDictionaryType dict => MapDictionaryType(dict),
            IrNullableType nullable => MapWrapperType("std::optional", nullable.InnerType),
            IrOptionalType optional => MapWrapperType("std::optional", optional.InnerType),
            IrUnionType union => MapUnionType(union),
            IrIntersectionType intersection => MapIntersectionType(intersection),
            _ => SimpleType("std::string"),
        };
    }

    /// <summary>
    /// Примитивы: String→std::string, Integer/Long→int32_t/int64_t, Number→double,
    /// Boolean→bool, DateTime/Date/Time/Uuid→std::string (ISO 8601 / текст — round-trip fidelity),
    /// Binary/ByteArray/File→std::vector&lt;std::uint8_t&gt;, Any→std::string + warning,
    /// Void→void.
    /// </summary>
    public TypeNode MapPrimitiveType(IrPrimitiveType primitive)
    {
        return primitive.PrimitiveKind switch
        {
            IrPrimitiveKind.String => SimpleType("std::string"),
            IrPrimitiveKind.Number => SimpleType("double", isPrimitive: true),
            IrPrimitiveKind.Integer => SimpleType("std::int32_t", isPrimitive: true),
            IrPrimitiveKind.Long => SimpleType("std::int64_t", isPrimitive: true),
            IrPrimitiveKind.Boolean => SimpleType("bool", isPrimitive: true),
            IrPrimitiveKind.DateTime => SimpleType("std::string"),
            IrPrimitiveKind.Date => SimpleType("std::string"),
            IrPrimitiveKind.Time => SimpleType("std::string"),
            IrPrimitiveKind.Uuid => SimpleType("std::string"),
            IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File
                => GenericType("std::vector", SimpleType("std::uint8_t", isPrimitive: true)),
            IrPrimitiveKind.Any => WarnedString(primitive, "Any"),
            IrPrimitiveKind.Void => SimpleType("void", isPrimitive: true),
            _ => SimpleType("std::string"),
        };
    }

    /// <summary>
    /// Ссылка на модель/enum: имя без префиксов (PascalCase).
    /// </summary>
    public TypeNode MapModelReference(IrModelReference modelRef)
    {
        var isEnum = _enumNames != null && _enumNames.Contains(modelRef.ModelName);
        var typeName = isEnum ? _naming.EnumName(modelRef.ModelName) : _naming.StructName(modelRef.ModelName);
        return SimpleType(typeName);
    }

    public bool IsEnumReference(string modelName)
        => _enumNames != null && _enumNames.Contains(modelName);

    public TypeNode MapArrayType(IrArrayType array)
        => GenericType("std::vector", MapType(array.ElementType));

    public TypeNode MapDictionaryType(IrDictionaryType dict)
        => GenericType("std::unordered_map", MapType(dict.KeyType), MapType(dict.ValueType));

    /// <summary>
    /// Nullable/Optional → std::optional&lt;Inner&gt; — в отличие от UE, std-C++ моделирует
    /// опциональность явно (модель знает, что ключ можно опустить).
    /// </summary>
    public TypeNode MapWrapperType(string wrapper, IrType inner)
        => GenericType(wrapper, MapType(inner));

    /// <summary>Union: прямого аналога нет; v1 — std::string (сквозной raw JSON) + warning.</summary>
    public TypeNode MapUnionType(IrUnionType union)
    {
        AddWarning($"union type serialized as raw JSON string ({union.Types.Count} variants; typed variant — backlog)");
        return SimpleType("std::string");
    }

    /// <summary>Intersection: v1 — std::string (raw JSON) + warning.</summary>
    public TypeNode MapIntersectionType(IrIntersectionType intersection)
    {
        AddWarning($"intersection type serialized as raw JSON string ({intersection.Types.Count} members)");
        return SimpleType("std::string");
    }

    /// <summary>
    /// Default member initializer: числам/bool — литерал, остальным — «{}» (заполняет билдер).
    /// </summary>
    public string? GetDefaultValue(IrType irType)
    {
        return irType switch
        {
            IrPrimitiveType p => p.PrimitiveKind switch
            {
                IrPrimitiveKind.Number => "0.0",
                IrPrimitiveKind.Integer or IrPrimitiveKind.Long => "0",
                IrPrimitiveKind.Boolean => "false",
                _ => null,
            },
            IrNullableType n => GetDefaultValue(n.InnerType),
            IrOptionalType o => GetDefaultValue(o.InnerType),
            _ => null,
        };
    }

    /// <summary>
    /// Выражение литерала для query/path-параметра (инвариантное строковое представление):
    /// bool → тернарник, целые → std::to_string, enum → std::string(ToWire(x)),
    /// double → std::to_string (6 знаков; v1), строки/dates/uuid — как есть.
    /// </summary>
    public string FormatQueryLiteral(IrType irType, string expression)
    {
        return irType switch
        {
            IrPrimitiveType p => p.PrimitiveKind switch
            {
                IrPrimitiveKind.Boolean => $"({expression} ? \"true\" : \"false\")",
                IrPrimitiveKind.Integer or IrPrimitiveKind.Long or IrPrimitiveKind.Number => $"std::to_string({expression})",
                _ => expression,
            },
            IrModelReference modelRef when IsEnumReference(modelRef.ModelName)
                => $"std::string(ToWire({expression}))",
            IrModelReference
                => $"ValIO::Dump({expression})", // модель в query-параметре — JSON-строка (сериализатор включён в клиентском .cpp)
            IrNullableType n => FormatQueryLiteral(n.InnerType, expression),
            IrOptionalType o => FormatQueryLiteral(o.InnerType, expression),
            _ => expression,
        };
    }

    /// <summary>
    /// Стандартные include, необходимые модели с данным типом поля (без учёта ссылок
    /// на другие модели — их добавляет CppTypeReferenceCollector).
    /// </summary>
    public static IReadOnlyList<string> StdIncludesFor(IrType irType)
    {
        var includes = new SortedSet<string>(StringComparer.Ordinal);
        CollectStdIncludes(irType, includes);
        return [.. includes];
    }

    private static void CollectStdIncludes(IrType irType, ISet<string> includes)
    {
        switch (irType)
        {
            case IrPrimitiveType p:
                switch (p.PrimitiveKind)
                {
                    case IrPrimitiveKind.String or IrPrimitiveKind.DateTime or IrPrimitiveKind.Date
                        or IrPrimitiveKind.Time or IrPrimitiveKind.Uuid or IrPrimitiveKind.Any:
                        includes.Add("<string>");
                        break;
                    case IrPrimitiveKind.Integer or IrPrimitiveKind.Long:
                        includes.Add("<cstdint>");
                        break;
                    case IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File:
                        includes.Add("<vector>");
                        includes.Add("<cstdint>");
                        break;
                }
                break;
            case IrArrayType:
                includes.Add("<vector>");
                CollectStdIncludes(((IrArrayType)irType).ElementType, includes);
                break;
            case IrDictionaryType dict:
                includes.Add("<unordered_map>");
                CollectStdIncludes(dict.KeyType, includes);
                CollectStdIncludes(dict.ValueType, includes);
                break;
            case IrNullableType nullable:
                includes.Add("<optional>");
                CollectStdIncludes(nullable.InnerType, includes);
                break;
            case IrOptionalType optional:
                includes.Add("<optional>");
                CollectStdIncludes(optional.InnerType, includes);
                break;
            case IrUnionType or IrIntersectionType:
                includes.Add("<string>");
                break;
            // IrModelReference — собственный хедер модели, std-инклуды не нужны.
        }
    }

    /// <summary>Файловый/бинарный тип (multipart-части) — общая реализация Core.</summary>
    public static bool IsFileOrBinaryType(IrType irType) => IrTypeQueries.IsFileOrBinary(irType);

    private TypeNode WarnedString(IrType irType, string kind)
    {
        AddWarning($"{kind} fields map to std::string (raw JSON pass-through)");
        return SimpleType("std::string");
    }

    private void AddWarning(string message) => Warnings.Add(message);

    private static SimpleTypeNode SimpleType(string name, bool isPrimitive = false)
        => new(new IdentifierNode(name), isPrimitive: isPrimitive);

    private static GenericTypeNode GenericType(string name, params TypeNode[] typeArgs)
        => new(new IdentifierNode(name), typeArgs);
}
