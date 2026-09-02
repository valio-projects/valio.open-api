using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Core.IR.Utilities;

/// <summary>
/// Общие запросы над закрытой иерархией IrType (без привязки к языку).
/// </summary>
public static class IrTypeQueries
{
    /// <summary>
    /// Разворачивает обёртки Nullable/Optional до «внутреннего» типа.
    /// </summary>
    public static IrType UnwrapNullableOptional(IrType type)
    {
        while (type is IrNullableType nullable)
            type = nullable.InnerType;
        while (type is IrOptionalType optional)
            type = optional.InnerType;
        return type;
    }

    /// <summary>
    /// Является ли тип файловым/бинарным (byte-массивом или их массивом) —
    /// используется для multipart-частей и binary-тел. Единая реализация
    /// для всех генераторов (раньше дублировалась в UETypeMapper).
    /// </summary>
    public static bool IsFileOrBinary(IrType type)
    {
        var unwrapped = UnwrapNullableOptional(type);

        return unwrapped switch
        {
            IrPrimitiveType p => p.PrimitiveKind is IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File,
            IrArrayType a when a.ElementType is IrPrimitiveType ep => ep.PrimitiveKind is IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File,
            _ => false,
        };
    }
}
