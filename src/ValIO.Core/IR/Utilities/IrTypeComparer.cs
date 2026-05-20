using ValIO.Core.IR.Types;

namespace ValIO.Core.IR.Utilities;

/// <summary>
/// Утилита для семантического сравнения IrType.
/// </summary>
public static class IrTypeComparer
{
    /// <summary>
    /// Сравнивает два IrType на семантическое равенство.
    /// Упрощённое сравнение для предупреждений о дубликатах параметров.
    /// </summary>
    public static bool AreSemanticallyEqual(IrType? a, IrType? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        var coreA = UnwrapOptionalOrNullable(a);
        var coreB = UnwrapOptionalOrNullable(b);

        return (coreA, coreB) switch
        {
            (IrPrimitiveType pa, IrPrimitiveType pb) => pa.PrimitiveKind == pb.PrimitiveKind,
            (IrModelReference ma, IrModelReference mb) => ma.ModelName == mb.ModelName,
            (IrArrayType aa, IrArrayType ab) => AreSemanticallyEqual(aa.ElementType, ab.ElementType),
            _ => a.Equals(b)
        };
    }

    private static IrType UnwrapOptionalOrNullable(IrType type)
    {
        return type switch
        {
            IrNullableType n => UnwrapOptionalOrNullable(n.InnerType),
            IrOptionalType o => UnwrapOptionalOrNullable(o.InnerType),
            _ => type
        };
    }
}
