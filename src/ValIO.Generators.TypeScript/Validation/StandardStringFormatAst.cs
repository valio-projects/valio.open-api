using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Options;
using static ValIO.Generators.TypeScript.Validation.ValidationAst;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>Builds shared standard string format predicates using ordinary TypeScript AST expressions.</summary>
internal static class StandardStringFormatAst
{
    /// <summary>Matches a four-digit date before checking month and leap-year validity.</summary>
    private const string DatePattern = @"^\d{4}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12]\d|3[01])$";

    /// <summary>Matches an RFC 3339 full time, including an explicit offset and optional fractional seconds.</summary>
    private const string TimePattern = @"(?:[01]\d|2[0-3]):[0-5]\d:(?:[0-5]\d|60)(?:\.\d+)?(?:[Zz]|[+-](?:[01]\d|2[0-3]):[0-5]\d)";

    /// <summary>Builds a complete standard format callback for registry-based validators.</summary>
    /// <param name="format">The standard format name.</param>
    /// <returns>The inferred string predicate callback.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The format is unsupported.</exception>
    public static ArrowFunctionExpressionNode Build(string format)
    {
        var value = Id("value");
        return Predicate(format switch
        {
            "uuid" => Matches(TypeScriptOptionIds.UuidFormat.Pattern, value),
            "date" => CalendarDate(value),
            "time" => Matches($"^{TimePattern}$", value),
            "date-time" => And(Matches($"^\\d{{4}}-\\d{{2}}-\\d{{2}}[Tt]{TimePattern}$", value),
                CalendarDate(Call(Member(value, "slice"), Number(0), Number(10)))),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported standard string format.")
        });
    }

    /// <summary>Builds an additional calendar check for validators whose ISO checks accept nonexistent dates.</summary>
    /// <param name="timestamp">Whether the input contains a timestamp after its date prefix.</param>
    /// <returns>The calendar predicate callback.</returns>
    public static ArrowFunctionExpressionNode CalendarDatePredicate(bool timestamp = false) => Predicate(CalendarDate(
        timestamp ? Call(Member(Id("value"), "slice"), Number(0), Number(10)) : Id("value")));

    /// <summary>Checks lexical shape and rejects dates normalized into another calendar day.</summary>
    /// <param name="value">The string date expression.</param>
    /// <returns>A predicate that rejects invalid dates without throwing from toISOString.</returns>
    private static ExpressionNode CalendarDate(ExpressionNode value)
    {
        var validTime = new UnaryExpressionNode(UnaryOperator.LogicalNot,
            Call(Member(Id("Number"), "isNaN"), Call(Member(Id("Date"), "parse"), value)));
        var date = new CallExpressionNode(Id("Date"), [value], isNewExpression: true);
        var normalized = Call(Member(Call(Member(date, "toISOString")), "slice"), Number(0), Number(10));
        return And(Matches(DatePattern, value), And(validTime, new BinaryExpressionNode(normalized, BinaryOperator.Equal, value)));
    }

    /// <summary>Builds a callback with an inferred string parameter.</summary>
    /// <param name="body">The predicate result.</param>
    /// <returns>The callback expression.</returns>
    private static ArrowFunctionExpressionNode Predicate(ExpressionNode body) => new([
        new ParameterDeclarationNode(new IdentifierNode("value"), null, isRequired: true)
    ], body);

    /// <summary>Builds an ECMAScript regular-expression test.</summary>
    /// <param name="pattern">The anchored pattern source.</param>
    /// <param name="value">The tested string expression.</param>
    /// <returns>The regular-expression predicate.</returns>
    private static ExpressionNode Matches(string pattern, ExpressionNode value) => Call(Member(
        new CallExpressionNode(Id("RegExp"), [new LiteralExpressionNode(pattern, LiteralKind.String)], isNewExpression: true), "test"), value);

    /// <summary>Combines predicates with short-circuit logical conjunction.</summary>
    /// <param name="left">The first predicate.</param>
    /// <param name="right">The second predicate.</param>
    /// <returns>The conjunction expression.</returns>
    private static ExpressionNode And(ExpressionNode left, ExpressionNode right) => new BinaryExpressionNode(left, BinaryOperator.LogicalAnd, right);

    /// <summary>Creates an integer literal.</summary>
    /// <param name="value">The integer value.</param>
    /// <returns>The literal node.</returns>
    private static LiteralExpressionNode Number(int value) => new(value, LiteralKind.Number);
}
