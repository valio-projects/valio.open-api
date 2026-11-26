using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Visitors;

namespace ValIO.Generators.Python.Ast.Nodes;

/// <summary>Represents a Python dictionary display.</summary>
/// <param name="items">The key and value expressions in source order.</param>
public sealed class PythonDictionaryExpressionNode(IReadOnlyList<(ExpressionNode Key, ExpressionNode Value)> items) : ExpressionNode
{
    /// <summary>Gets the key and value expressions in source order.</summary>
    /// <value>The key and value expressions in source order.</value>
    public IReadOnlyList<(ExpressionNode Key, ExpressionNode Value)> Items { get; } = items.ToArray();

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => Items.SelectMany(item => new AstNode[] { item.Key, item.Value }).ToArray();

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IPythonAstVisitor<TResult> python ? python.VisitPythonDictionaryExpression(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new PythonDictionaryExpressionNode(Items.Select(item => ((ExpressionNode)item.Key.Clone(), (ExpressionNode)item.Value.Clone())).ToArray());
}
