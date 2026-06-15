using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>
/// Небольшие фабрики выражений AST, общие для стратегий валидации.
/// </summary>
internal static class ValidationAst
{
    public static IdentifierExpressionNode Id(string name) => new(new IdentifierNode(name));

    public static MemberAccessExpressionNode Member(ExpressionNode target, string name) =>
        new(target, new IdentifierNode(name));

    public static CallExpressionNode Call(ExpressionNode target, params ExpressionNode[] arguments) =>
        new(target, arguments.ToImmutableList());
}
