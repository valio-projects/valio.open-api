using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Options;
using static ValIO.Generators.TypeScript.Validation.ValidationAst;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>
/// Строит выражение Zod-схемы (<see cref="ExpressionNode"/>) из <see cref="IrType"/>:
/// ссылки на модель → <c>{Model}Schema</c>, union → <c>z.union([...])</c>, array → <c>z.array(...)</c> и т.д.
/// Член-схемы ссылаются по имени <c>{Model}Schema</c> и импортируются отдельно.
/// </summary>
internal sealed class ZodSchemaExpressionBuilder
{
    private readonly string _uuidFormat;

    public ZodSchemaExpressionBuilder(string? uuidFormat = null)
    {
        _uuidFormat = TypeScriptOptionIds.UuidFormat.Resolve(uuidFormat);
    }

    public ExpressionNode Build(IrType type) =>
        type switch
        {
            IrModelReference reference => Id($"{reference.ModelName}Schema"),
            IrArrayType array => Call(Member(Id("z"), "array"), Build(array.ElementType)),
            IrDictionaryType dictionary => Call(Member(Id("z"), "record"), Build(dictionary.ValueType)),
            IrNullableType nullable => Call(Member(Build(nullable.InnerType), "nullable")),
            IrOptionalType optional => Call(Member(Build(optional.InnerType), "optional")),
            IrUnionType union => Call(
                Member(Id("z"), "union"),
                new ArrayLiteralExpressionNode(union.Types.Select(Build).ToImmutableList())),
            IrIntersectionType intersection => BuildIntersection(intersection.Types),
            IrPrimitiveType primitive => BuildPrimitive(primitive.PrimitiveKind),
            _ => Call(Member(Id("z"), "unknown"))
        };

    private ExpressionNode BuildIntersection(IReadOnlyList<IrType> types)
    {
        // z.intersection(a, b); для >2 членов сворачиваем через .and(...).
        if (types.Count == 0)
            return Call(Member(Id("z"), "unknown"));
        if (types.Count == 1)
            return Build(types[0]);

        var result = Call(Member(Id("z"), "intersection"), Build(types[0]), Build(types[1]));
        for (var i = 2; i < types.Count; i++)
            result = Call(Member(result, "and"), Build(types[i]));
        return result;
    }

    private ExpressionNode BuildPrimitive(IrPrimitiveKind kind) =>
        kind switch
        {
            IrPrimitiveKind.String => Call(Member(Id("z"), "string")),
            IrPrimitiveKind.Number => Call(Member(Id("z"), "number")),
            IrPrimitiveKind.Integer or IrPrimitiveKind.Long => Call(Member(Call(Member(Id("z"), "number")), "int")),
            IrPrimitiveKind.Boolean => Call(Member(Id("z"), "boolean")),
            IrPrimitiveKind.DateTime or IrPrimitiveKind.Date or IrPrimitiveKind.Time => Call(Member(Id("z"), "string")),
            IrPrimitiveKind.Uuid => _uuidFormat switch
            {
                TypeScriptOptionIds.UuidFormat.String => Call(Member(Id("z"), "string")),
                TypeScriptOptionIds.UuidFormat.Regex => Call(Member(Call(Member(Id("z"), "string")), "regex"),
                    Id(TypeScriptOptionIds.UuidFormat.RegexConstName)),
                _ => Call(Member(Call(Member(Id("z"), "string")), "uuid"))
            },
            IrPrimitiveKind.Void => Call(Member(Id("z"), "void")),
            _ => Call(Member(Id("z"), "unknown"))
        };
}
