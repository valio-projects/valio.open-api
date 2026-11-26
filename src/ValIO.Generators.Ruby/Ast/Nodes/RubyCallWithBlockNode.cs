using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Ruby.Ast.Visitors;

namespace ValIO.Generators.Ruby.Ast.Nodes;

/// <summary>Represents a Ruby call followed by a block that receives one argument.</summary>
/// <param name="call">The invocation.</param>
/// <param name="parameter">The block parameter.</param>
/// <param name="body">The block statements.</param>
public sealed class RubyCallWithBlockNode(CallExpressionNode call, IdentifierNode parameter, BlockNode body) : ExpressionNode
{
    /// <summary>Gets the invocation.</summary>
    /// <value>The invocation.</value>
    public CallExpressionNode Call { get; } = call;

    /// <summary>Gets the block parameter.</summary>
    /// <value>The block parameter.</value>
    public IdentifierNode Parameter { get; } = parameter;

    /// <summary>Gets the block statements.</summary>
    /// <value>The block statements.</value>
    public BlockNode Body { get; } = body;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Call, Parameter, Body];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IRubyAstVisitor<TResult> ruby ? ruby.VisitRubyCallWithBlock(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new RubyCallWithBlockNode((CallExpressionNode)Call.Clone(), (IdentifierNode)Parameter.Clone(), (BlockNode)Body.Clone());
}
