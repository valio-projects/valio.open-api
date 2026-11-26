using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>Represents Rust's unit type, the empty tuple.</summary>
public sealed class RustUnitTypeNode : TypeNode
{
    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children => [];

    /// <inheritdoc />
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IRustAstVisitor<TResult> rust ? rust.VisitRustUnitType(this) : visitor.DefaultVisit(this);

    /// <inheritdoc />
    public override AstNode Clone() => new RustUnitTypeNode();
}
