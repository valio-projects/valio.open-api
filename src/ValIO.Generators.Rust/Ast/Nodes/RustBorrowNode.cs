using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>Borrows a Rust value by shared reference.</summary>
/// <param name="operand">The value to borrow.</param>
public sealed class RustBorrowNode(ExpressionNode operand) : ExpressionNode
{
    /// <summary>Gets the value to borrow.</summary>
    /// <value>The value to borrow.</value>
    public ExpressionNode Operand { get; } = operand;

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children => [Operand];

    /// <inheritdoc />
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IRustAstVisitor<TResult> rust ? rust.VisitRustBorrow(this) : visitor.DefaultVisit(this);

    /// <inheritdoc />
    public override AstNode Clone() => new RustBorrowNode((ExpressionNode)Operand.Clone());
}
