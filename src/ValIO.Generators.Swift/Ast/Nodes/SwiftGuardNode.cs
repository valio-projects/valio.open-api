using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>Exits the enclosing scope when a guard condition cannot be satisfied.</summary>
/// <param name="conditions">The conditions and optional bindings.</param>
/// <param name="otherwise">The exit block.</param>
public sealed class SwiftGuardNode(IReadOnlyList<ExpressionNode> conditions, BlockNode otherwise) : StatementNode
{
    /// <summary>Gets the conditions and optional bindings.</summary>
    /// <value>The conditions and optional bindings.</value>
    public IReadOnlyList<ExpressionNode> Conditions { get; } = conditions.ToArray();

    /// <summary>Gets the exit block.</summary>
    /// <value>The exit block.</value>
    public BlockNode Otherwise { get; } = otherwise;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [.. Conditions, Otherwise];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ISwiftAstVisitor<TResult> swift ? swift.VisitSwiftGuard(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new SwiftGuardNode(Conditions.Select(condition => (ExpressionNode)condition.Clone()).ToArray(), (BlockNode)Otherwise.Clone());
}
