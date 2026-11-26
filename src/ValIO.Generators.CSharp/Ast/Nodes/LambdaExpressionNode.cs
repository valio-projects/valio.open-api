using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Visitors;

namespace ValIO.Generators.CSharp.Ast.Nodes;

/// <summary>Represents a synchronous C# lambda with an expression body.</summary>
/// <param name="parameter">the inferred parameter.</param>
/// <param name="body">the returned expression.</param>
public sealed class LambdaExpressionNode(IdentifierNode parameter, ExpressionNode body) : ExpressionNode
{
    /// <summary>Gets the inferred parameter.</summary>
    /// <value>the inferred parameter.</value>
    public IdentifierNode Parameter { get; } = parameter;

    /// <summary>Gets the returned expression.</summary>
    /// <value>the returned expression.</value>
    public ExpressionNode Body { get; } = body;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Parameter, Body];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ICSharpAstVisitor<TResult> csharp
            ? csharp.VisitLambdaExpression(this)
            : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new LambdaExpressionNode((IdentifierNode)Parameter.Clone(), (ExpressionNode)Body.Clone());
}
