using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Kotlin.Ast.Builders;

/// <summary>
/// Walks IR types and collects required Kotlin imports.
/// </summary>
internal static class KotlinTypeImportScanner
{
    /// <summary>Standard imports that depend purely on primitive kinds, plus referenced model names.</summary>
    public sealed class Result
    {
        public SortedSet<string> StandardImports { get; } = new(StringComparer.Ordinal);

        /// <summary>Referenced model names WITH affix (used as the imported class identifier).</summary>
        public SortedSet<string> ModelReferences { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Maps an affixed referenced model name (as found in <see cref="ModelReferences"/>) to its
        /// ORIGINAL (pre-affix) name. Self-seeded while walking <see cref="IrModelReference"/> nodes.
        /// Import PATHS must use the original so generated file names stay stable under affixing.
        /// </summary>
        public Dictionary<string, string> OriginalModelNames { get; } = new(StringComparer.Ordinal);
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
                    case IrPrimitiveKind.Date:     result.StandardImports.Add("kotlinx.datetime.LocalDate"); break;
                    case IrPrimitiveKind.DateTime: result.StandardImports.Add("kotlinx.datetime.Instant"); break;
                    case IrPrimitiveKind.Time:     result.StandardImports.Add("kotlinx.datetime.LocalTime"); break;
                    case IrPrimitiveKind.Uuid:     result.StandardImports.Add("java.util.UUID"); break;
                    case IrPrimitiveKind.File:     result.StandardImports.Add("java.io.File"); break;
                }
                break;
            case IrModelReference r:
                result.ModelReferences.Add(r.ModelName);
                result.OriginalModelNames[r.ModelName] = r.OriginalModelName;
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
