using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>Unwraps a result or propagates its error from the enclosing function.</summary>
/// <param name="operand">The result to unwrap.</param>
public sealed class RustTryNode(ExpressionNode operand) : ExpressionNode
{
    /// <summary>Gets the result to unwrap.</summary>
    /// <value>The result to unwrap.</value>
    public ExpressionNode Operand { get; } = operand;

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children => [Operand];

    /// <inheritdoc />
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IRustAstVisitor<TResult> rust ? rust.VisitRustTry(this) : visitor.DefaultVisit(this);

    /// <inheritdoc />
    public override AstNode Clone() => new RustTryNode((ExpressionNode)Operand.Clone());
}
