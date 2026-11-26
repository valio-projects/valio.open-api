using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Nodes;

/// <summary>Represents an F# task computation expression.</summary>
/// <param name="body">The ordered statements of the computation.</param>
public sealed class FSharpTaskExpressionNode(BlockNode body) : ExpressionNode
{
    /// <summary>Gets the ordered statements of the computation.</summary>
    /// <value>The ordered statements of the computation.</value>
    public BlockNode Body { get; } = body;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Body];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IFSharpAstVisitor<TResult> fsharp ? fsharp.VisitFSharpTaskExpression(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new FSharpTaskExpressionNode((BlockNode)Body.Clone());
}
