using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>
/// Классификация IR-типов для валидации клиента: какие типы стоит валидировать, какие требуют
/// вынесения в именованную схему, и какие model-схемы они импортируют.
/// </summary>
internal static class ValidationTypes
{
    /// <summary>
    /// Имеет ли смысл валидировать тип. Для <see cref="IrPrimitiveKind.Any"/>/<see cref="IrPrimitiveKind.Void"/>
    /// схема бесполезна.
    /// </summary>
    public static bool IsValidatable(IrType type)
        => Core(type) is not IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Any or IrPrimitiveKind.Void };

    /// <summary>
    /// Требует ли тип ВЫНЕСЕНИЯ в именованную схему (например в <c>{op}.results.schemas.ts</c>):
    /// всё, что строит выражение схемы (union/array/nullable-ref/...) — кроме голой ссылки на модель,
    /// которая уже именована (<c>{Model}Schema</c>).
    /// </summary>
    public static bool NeedsConstructedSchema(IrType type)
        => IsValidatable(type) && type is not IrModelReference;

    /// <summary>
    /// Собирает имена model-схем (<c>{Model}Schema</c>), на которые ссылается тип — рекурсивно по
    /// union/intersection/array/dictionary/nullable/optional.
    /// </summary>
    public static void CollectModelSchemaNames(IrType type, ISet<string> names)
    {
        switch (type)
        {
            case IrModelReference reference:
                names.Add(reference.ModelName);
                break;
            case IrArrayType array:
                CollectModelSchemaNames(array.ElementType, names);
                break;
            case IrDictionaryType dictionary:
                CollectModelSchemaNames(dictionary.ValueType, names);
                break;
            case IrNullableType nullable:
                CollectModelSchemaNames(nullable.InnerType, names);
                break;
            case IrOptionalType optional:
                CollectModelSchemaNames(optional.InnerType, names);
                break;
            case IrUnionType union:
                foreach (var member in union.Types)
                    CollectModelSchemaNames(member, names);
                break;
            case IrIntersectionType intersection:
                foreach (var member in intersection.Types)
                    CollectModelSchemaNames(member, names);
                break;
        }
    }

    private static IrType Core(IrType type)
    {
        var core = type;
        while (true)
        {
            if (core is IrNullableType n) { core = n.InnerType; continue; }
            if (core is IrOptionalType o) { core = o.InnerType; continue; }
            break;
        }

        return core;
    }
}
