using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Visitors;

namespace ValIO.Generators.CSharp.Ast.Nodes;

/// <summary>Represents the postfix suppression of nullable analysis.</summary>
/// <param name="operand">the expression whose nullable warning is suppressed.</param>
public sealed class NullForgivingExpressionNode(ExpressionNode operand) : ExpressionNode
{
    /// <summary>Gets the expression whose nullable warning is suppressed.</summary>
    /// <value>the expression whose nullable warning is suppressed.</value>
    public ExpressionNode Operand { get; } = operand;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Operand];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ICSharpAstVisitor<TResult> csharp
            ? csharp.VisitNullForgivingExpression(this)
            : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new NullForgivingExpressionNode((ExpressionNode)Operand.Clone());
}
