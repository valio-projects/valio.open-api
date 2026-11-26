using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

/// <summary>References a member type on a TypeScript object type.</summary>
/// <param name="receiver">The type whose member is selected.</param>
/// <param name="index">The literal or key type used to select the member.</param>
public sealed class IndexedAccessTypeNode(TypeNode receiver, TypeNode index) : TypeNode
{
    /// <summary>Gets the indexed type.</summary>
    /// <value>The source type of the member.</value>
    public TypeNode Receiver { get; } = receiver;

    /// <summary>Gets the key type.</summary>
    /// <value>The key or union of keys used in the index.</value>
    public TypeNode Index { get; } = index;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Receiver, Index];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ITypeScriptAstVisitor<TResult> typeScript ? typeScript.VisitIndexedAccessType(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new IndexedAccessTypeNode((TypeNode)Receiver.Clone(), (TypeNode)Index.Clone());
}
