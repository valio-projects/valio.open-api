using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>Represents a Kotlin invocation with a trailing lambda.</summary>
/// <param name="call">The invocation and its ordinary arguments.</param>
/// <param name="body">The statements and final value of the lambda.</param>
/// <param name="parameter">The optional explicit lambda parameter.</param>
public sealed class KotlinTrailingLambdaCallNode(CallExpressionNode call, BlockNode body, IdentifierNode? parameter = null) : ExpressionNode
{
    /// <summary>Gets the called method and its ordinary arguments.</summary>
    /// <value>The invocation before the lambda.</value>
    public CallExpressionNode Call { get; } = call;

    /// <summary>Gets the lambda's statements.</summary>
    /// <value>The executable block.</value>
    public BlockNode Body { get; } = body;

    /// <summary>Gets the optional explicit argument bound by the lambda.</summary>
    /// <value>The identifier available inside the lambda when specified.</value>
    public IdentifierNode? Parameter { get; } = parameter;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => Parameter is null ? [Call, Body] : [Call, Parameter, Body];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IKotlinAstVisitor<TResult> kotlin ? kotlin.VisitKotlinTrailingLambdaCall(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new KotlinTrailingLambdaCallNode(
        (CallExpressionNode)Call.Clone(), (BlockNode)Body.Clone(), (IdentifierNode?)Parameter?.Clone());
}
