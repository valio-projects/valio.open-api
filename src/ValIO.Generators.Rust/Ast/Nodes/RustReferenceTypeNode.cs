using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>Represents a shared Rust reference to another type.</summary>
/// <param name="targetType">The borrowed type.</param>
public sealed class RustReferenceTypeNode(TypeNode targetType) : TypeNode
{
    /// <summary>Gets the borrowed type.</summary>
    public TypeNode TargetType { get; } = targetType;

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children => [TargetType];

    /// <inheritdoc />
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IRustAstVisitor<TResult> rust ? rust.VisitRustReferenceType(this) : visitor.DefaultVisit(this);

    /// <inheritdoc />
    public override AstNode Clone() => new RustReferenceTypeNode((TypeNode)TargetType.Clone());
}
