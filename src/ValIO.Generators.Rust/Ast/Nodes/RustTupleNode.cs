using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>Represents a Rust tuple, including the unit value.</summary>
/// <param name="elements">The ordered tuple elements.</param>
public sealed class RustTupleNode(IReadOnlyList<ExpressionNode> elements) : ExpressionNode
{
    /// <summary>Gets the ordered tuple elements.</summary>
    /// <value>The ordered tuple elements.</value>
    public IReadOnlyList<ExpressionNode> Elements { get; } = elements.ToArray();

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children => Elements;

    /// <inheritdoc />
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IRustAstVisitor<TResult> rust ? rust.VisitRustTuple(this) : visitor.DefaultVisit(this);

    /// <inheritdoc />
    public override AstNode Clone() => new RustTupleNode(Elements.Select(element => (ExpressionNode)element.Clone()).ToArray());
}
