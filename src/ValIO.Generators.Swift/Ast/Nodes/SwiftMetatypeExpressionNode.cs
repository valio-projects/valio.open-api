using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>References the value representing a type at runtime.</summary>
/// <param name="referencedType">The referenced type.</param>
public sealed class SwiftMetatypeExpressionNode(TypeNode referencedType) : ExpressionNode
{
    /// <summary>Gets the referenced type.</summary>
    /// <value>The referenced type.</value>
    public TypeNode ReferencedType { get; } = referencedType;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [ReferencedType];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ISwiftAstVisitor<TResult> swift ? swift.VisitSwiftMetatypeExpression(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new SwiftMetatypeExpressionNode((TypeNode)ReferencedType.Clone());
}
