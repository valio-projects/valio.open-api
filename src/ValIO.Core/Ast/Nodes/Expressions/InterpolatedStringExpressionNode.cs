using System.Collections.Immutable;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Expressions;

/// <summary>Represents ordered literal segments and expressions embedded in a string.</summary>
/// <param name="parts">The literal segments and interpolation operands.</param>
public sealed class InterpolatedStringExpressionNode(IReadOnlyList<ExpressionNode> parts) : ExpressionNode
{
    /// <summary>Gets the interleaved text literals and expressions.</summary>
    /// <value>The original segment order.</value>
    public IReadOnlyList<ExpressionNode> Parts { get; } = parts.ToImmutableList();

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => Parts;

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) => visitor.VisitInterpolatedStringExpression(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new InterpolatedStringExpressionNode(
        Parts.Select(part => (ExpressionNode)part.Clone()).ToArray());
}
