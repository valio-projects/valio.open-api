using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>Executes a block after binding the present value of a Rust option.</summary>
/// <param name="binding">The name bound by the Some pattern.</param>
/// <param name="option">The optional value to match.</param>
/// <param name="body">The body executed for the Some case.</param>
public sealed class RustIfSomeNode(IdentifierNode binding, ExpressionNode option, BlockNode body) : StatementNode
{
    /// <summary>Gets the name bound by the Some pattern.</summary>
    /// <value>The name bound by the Some pattern.</value>
    public IdentifierNode Binding { get; } = binding;

    /// <summary>Gets the optional value to match.</summary>
    /// <value>The optional value to match.</value>
    public ExpressionNode Option { get; } = option;

    /// <summary>Gets the body executed for the Some case.</summary>
    /// <value>The body executed for the Some case.</value>
    public BlockNode Body { get; } = body;

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children => [Binding, Option, Body];

    /// <inheritdoc />
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IRustAstVisitor<TResult> rust ? rust.VisitRustIfSome(this) : visitor.DefaultVisit(this);

    /// <inheritdoc />
    public override AstNode Clone() => new RustIfSomeNode((IdentifierNode)Binding.Clone(), (ExpressionNode)Option.Clone(), (BlockNode)Body.Clone());
}
