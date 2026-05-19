using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Swift.Ast.Builders;

/// <summary>
/// Walks IR types and collects required Swift imports + model references.
/// Swift does not need per-type imports (Foundation covers Date/Data and the rest live in the
/// same module). The scanner still tracks model references so steps can emit cross-module imports
/// if/when that becomes relevant.
/// </summary>
internal static class SwiftTypeImportScanner
{
    public sealed class Result
    {
        public SortedSet<string> StandardImports { get; } = new(StringComparer.Ordinal);
        public SortedSet<string> ModelReferences { get; } = new(StringComparer.Ordinal);
    }

    public static Result Scan(params IrType[] roots)
    {
        var result = new Result();
        foreach (var r in roots)
            if (r is not null) Walk(r, result);
        return result;
    }

    public static void Walk(IrType type, Result result)
    {
        switch (type)
        {
            case IrPrimitiveType p:
                switch (p.PrimitiveKind)
                {
                    case IrPrimitiveKind.Date:
                    case IrPrimitiveKind.DateTime:
                    case IrPrimitiveKind.Time:
                    case IrPrimitiveKind.Binary:
                    case IrPrimitiveKind.ByteArray:
                    case IrPrimitiveKind.File:
                        result.StandardImports.Add("Foundation");
                        break;
                }
                break;
            case IrModelReference r:
                result.ModelReferences.Add(r.ModelName);
                break;
            case IrArrayType a:
                Walk(a.ElementType, result);
                break;
            case IrDictionaryType d:
                Walk(d.KeyType, result);
                Walk(d.ValueType, result);
                break;
            case IrNullableType n:
                Walk(n.InnerType, result);
                break;
            case IrOptionalType o:
                Walk(o.InnerType, result);
                break;
            case IrUnionType u:
                foreach (var t in u.Types) Walk(t, result);
                break;
            case IrIntersectionType i:
                foreach (var t in i.Types) Walk(t, result);
                break;
        }
    }
}
