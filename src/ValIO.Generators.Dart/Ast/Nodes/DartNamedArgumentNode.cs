using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Nodes;

/// <summary>Binds an expression to a named Dart call parameter.</summary>
/// <param name="name">The formal parameter name.</param>
/// <param name="value">The argument value.</param>
public sealed class DartNamedArgumentNode(IdentifierNode name, ExpressionNode value) : ExpressionNode
{
    /// <summary>Gets the formal parameter name.</summary>
    /// <value>The formal parameter name.</value>
    public IdentifierNode Name { get; } = name;

    /// <summary>Gets the argument value.</summary>
    /// <value>The argument value.</value>
    public ExpressionNode Value { get; } = value;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Name, Value];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IDartAstVisitor<TResult> dart
            ? dart.VisitDartNamedArgument(this)
            : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new DartNamedArgumentNode((IdentifierNode)Name.Clone(), (ExpressionNode)Value.Clone());
}
