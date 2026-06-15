using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Core.IR.Utilities;

/// <summary>
/// Нормализует композицию схем OpenAPI (allOf → intersection, oneOf/anyOf → union)
/// в корректные IR-типы. Единая точка нормализации для всех генераторов:
/// <list type="bullet">
/// <item>схлопывает вырожденные композиции из 0 элементов (→ any) и 1 элемента (→ сам элемент);</item>
/// <item>разворачивает вложенные композиции того же вида (allOf[allOf[X]] → X);</item>
/// <item>удаляет семантические дубликаты членов;</item>
/// <item>идемпотентно оборачивает результат в <see cref="IrNullableType"/>, если требуется.</item>
/// </list>
/// Это гарантирует инвариант «в IR не бывает intersection/union из &lt;2 типов»,
/// от которого зависят все 12 генераторов (single-element allOf из Swashbuckle
/// <c>UseAllOfToExtendReferenceSchemas</c> больше не создаёт вырожденный
/// <see cref="IrIntersectionType"/> и не теряет <c>nullable</c>).
/// </summary>
public static class IrCompositionNormalizer
{
    /// <summary>
    /// Нормализует allOf-композицию в intersection.
    /// 0 элементов → <see cref="IrPrimitiveKind.Any"/>; 1 элемент → сам элемент; ≥2 → <see cref="IrIntersectionType"/>.
    /// </summary>
    /// <param name="members">Уже построенные IR-типы членов allOf.</param>
    /// <param name="nullable">Допускает ли схема JSON null (sibling <c>nullable</c> / <c>x-nullable</c> / type-флаг Null).</param>
    public static IrType NormalizeIntersection(IReadOnlyList<IrType> members, bool nullable = false)
    {
        var flattened = Flatten(members, intersection: true);
        var distinct = Distinct(flattened);

        IrType core = distinct.Count switch
        {
            0 => new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Any },
            1 => distinct[0],
            _ => new IrIntersectionType { Types = distinct }
        };

        return nullable ? WrapNullable(core) : core;
    }

    /// <summary>
    /// Нормализует oneOf/anyOf-композицию в union.
    /// 0 элементов → <see cref="IrPrimitiveKind.Any"/>; 1 элемент → сам элемент; ≥2 → <see cref="IrUnionType"/>.
    /// </summary>
    /// <param name="members">Уже построенные IR-типы вариантов oneOf/anyOf (без чистых null-веток).</param>
    /// <param name="nullable">Допускает ли схема JSON null.</param>
    public static IrType NormalizeUnion(IReadOnlyList<IrType> members, bool nullable = false)
    {
        var flattened = Flatten(members, intersection: false);
        var distinct = Distinct(flattened);

        IrType core = distinct.Count switch
        {
            0 => new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Any },
            1 => distinct[0],
            _ => new IrUnionType { Types = distinct }
        };

        return (nullable || core is IrNullableType) ? WrapNullable(core) : core;
    }

    /// <summary>
    /// Идемпотентно оборачивает тип в <see cref="IrNullableType"/> (не создаёт двойную обёртку
    /// <c>IrNullableType{IrNullableType}</c>).
    /// </summary>
    public static IrType WrapNullable(IrType core)
        => core is IrNullableType ? core : new IrNullableType { InnerType = core };

    /// <summary>
    /// Разворачивает вложенные композиции того же вида в плоский список членов.
    /// </summary>
    private static List<IrType> Flatten(IReadOnlyList<IrType> members, bool intersection)
    {
        var result = new List<IrType>(members.Count);
        foreach (var member in members)
        {
            switch (member)
            {
                case IrIntersectionType i when intersection:
                    result.AddRange(Flatten(i.Types, intersection));
                    break;
                case IrUnionType u when !intersection:
                    result.AddRange(Flatten(u.Types, intersection));
                    break;
                default:
                    result.Add(member);
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Удаляет семантические дубликаты членов (одинаковые <c>$ref</c>/примитивы),
    /// сохраняя порядок первого вхождения.
    /// </summary>
    private static List<IrType> Distinct(IReadOnlyList<IrType> members)
    {
        var result = new List<IrType>(members.Count);
        foreach (var member in members)
        {
            var duplicate = false;
            foreach (var existing in result)
            {
                if (IrTypeComparer.AreSemanticallyEqual(existing, member))
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                result.Add(member);
        }

        return result;
    }
}
