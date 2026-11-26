using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Visitors;

namespace ValIO.Generators.CSharp.Ast.Nodes;

/// <summary>Represents a contextually typed C# collection expression.</summary>
/// <param name="elements">the elements in source order.</param>
public sealed class CollectionExpressionNode(IReadOnlyList<ExpressionNode> elements) : ExpressionNode
{
    /// <summary>Gets the elements in source order.</summary>
    /// <value>the elements in source order.</value>
    public IReadOnlyList<ExpressionNode> Elements { get; } = elements.ToArray();

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => Elements;

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ICSharpAstVisitor<TResult> csharp
            ? csharp.VisitCollectionExpression(this)
            : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new CollectionExpressionNode(Elements.Select(element => (ExpressionNode)element.Clone()).ToArray());
}
