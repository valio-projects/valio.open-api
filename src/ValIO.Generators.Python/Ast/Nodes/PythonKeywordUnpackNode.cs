using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Visitors;

namespace ValIO.Generators.Python.Ast.Nodes;

/// <summary>Passes a dictionary as the keyword arguments of a call.</summary>
/// <param name="dictionary">The dictionary of keyword values.</param>
public sealed class PythonKeywordUnpackNode(ExpressionNode dictionary) : ExpressionNode
{
    /// <summary>Gets the dictionary of keyword values.</summary>
    /// <value>The dictionary of keyword values.</value>
    public ExpressionNode Dictionary { get; } = dictionary;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Dictionary];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IPythonAstVisitor<TResult> python ? python.VisitPythonKeywordUnpack(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new PythonKeywordUnpackNode((ExpressionNode)Dictionary.Clone());
}
