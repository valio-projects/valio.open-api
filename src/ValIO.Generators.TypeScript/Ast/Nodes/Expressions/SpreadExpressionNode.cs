using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

/// <summary>
/// Spread expression для object/array literal (...expr)
/// </summary>
public sealed class SpreadExpressionNode : ExpressionNode
{
    /// <summary>
    /// Выражение для spread
    /// </summary>
    public ExpressionNode Expression { get; }

    public override IReadOnlyList<AstNode> Children => new[] { Expression };

    public SpreadExpressionNode(
        ExpressionNode expression,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> tsVisitor)
        {
            return tsVisitor.VisitSpreadExpression(this);
        }
        return visitor.DefaultVisit(this);
    }

    public override AstNode Clone()
    {
        return new SpreadExpressionNode(
            (ExpressionNode)Expression.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
