using ValIO.Core.IR;
using ValIO.Core.IR.Types;

namespace ValIO.Core.Tests.TestHelpers;

/// <summary>
/// Обходит все <see cref="IrType"/> в IR-документе (свойства и композиция моделей,
/// типы параметров/тел/ответов операций), включая вложенные обёртки.
/// Используется инвариантными тестами для проверки «в IR нет вырожденных композиций».
/// </summary>
internal static class IrTypeWalker
{
    public static IEnumerable<IrType> AllTypes(IrDocument document)
    {
        foreach (var model in document.Models)
        {
            foreach (var property in model.Properties)
                foreach (var t in Expand(property.Type))
                    yield return t;

            if (model.Composition != null)
                foreach (var member in model.Composition.Types)
                    foreach (var t in Expand(member))
                        yield return t;
        }

        foreach (var client in document.Clients)
        {
            foreach (var op in client.Operations)
            {
                foreach (var p in op.PathParameters.Concat(op.QueryParameters).Concat(op.HeaderParameters))
                    foreach (var t in Expand(p.Type))
                        yield return t;

                if (op.RequestBody != null)
                    foreach (var t in Expand(op.RequestBody.Type))
                        yield return t;

                foreach (var r in op.Responses)
                    if (r.Type != null)
                        foreach (var t in Expand(r.Type))
                            yield return t;
            }
        }
    }

    private static IEnumerable<IrType> Expand(IrType type)
    {
        yield return type;

        switch (type)
        {
            case IrArrayType a:
                foreach (var t in Expand(a.ElementType)) yield return t;
                break;
            case IrDictionaryType d:
                foreach (var t in Expand(d.KeyType)) yield return t;
                foreach (var t in Expand(d.ValueType)) yield return t;
                break;
            case IrNullableType n:
                foreach (var t in Expand(n.InnerType)) yield return t;
                break;
            case IrOptionalType o:
                foreach (var t in Expand(o.InnerType)) yield return t;
                break;
            case IrUnionType u:
                foreach (var member in u.Types)
                    foreach (var t in Expand(member)) yield return t;
                break;
            case IrIntersectionType i:
                foreach (var member in i.Types)
                    foreach (var t in Expand(member)) yield return t;
                break;
        }
    }
}
