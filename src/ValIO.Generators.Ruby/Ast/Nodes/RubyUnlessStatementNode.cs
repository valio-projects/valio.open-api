using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Ruby.Ast.Visitors;

namespace ValIO.Generators.Ruby.Ast.Nodes;

/// <summary>Executes a statement when its postfix condition is false.</summary>
/// <param name="statement">The guarded statement.</param>
/// <param name="condition">The condition that skips the statement.</param>
public sealed class RubyUnlessStatementNode(StatementNode statement, ExpressionNode condition) : StatementNode
{
    /// <summary>Gets the guarded statement.</summary>
    /// <value>The guarded statement.</value>
    public StatementNode Statement { get; } = statement;

    /// <summary>Gets the condition that skips the statement.</summary>
    /// <value>The condition that skips the statement.</value>
    public ExpressionNode Condition { get; } = condition;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Statement, Condition];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IRubyAstVisitor<TResult> ruby ? ruby.VisitRubyUnlessStatement(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new RubyUnlessStatementNode((StatementNode)Statement.Clone(), (ExpressionNode)Condition.Clone());
}
