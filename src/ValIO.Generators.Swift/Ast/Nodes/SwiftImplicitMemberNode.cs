using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>References a member of a type supplied by Swift's context.</summary>
/// <param name="name">The member identifier.</param>
public sealed class SwiftImplicitMemberNode(IdentifierNode name) : ExpressionNode
{
    /// <summary>Gets the member identifier.</summary>
    /// <value>The member identifier.</value>
    public IdentifierNode Name { get; } = name;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Name];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ISwiftAstVisitor<TResult> swift ? swift.VisitSwiftImplicitMember(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new SwiftImplicitMemberNode((IdentifierNode)Name.Clone());
}
