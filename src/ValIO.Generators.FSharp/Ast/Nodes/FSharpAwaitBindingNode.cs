using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Nodes;

/// <summary>Binds the result of asynchronous work inside a computation expression.</summary>
/// <param name="name">The binding identifier.</param>
/// <param name="computation">The computation whose result is bound.</param>
public sealed class FSharpAwaitBindingNode(IdentifierNode name, ExpressionNode computation) : StatementNode
{
    /// <summary>Gets the binding identifier.</summary>
    /// <value>The binding identifier.</value>
    public IdentifierNode Name { get; } = name;

    /// <summary>Gets the computation whose result is bound.</summary>
    /// <value>The computation whose result is bound.</value>
    public ExpressionNode Computation { get; } = computation;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Name, Computation];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IFSharpAstVisitor<TResult> fsharp ? fsharp.VisitFSharpAwaitBinding(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new FSharpAwaitBindingNode((IdentifierNode)Name.Clone(), (ExpressionNode)Computation.Clone());
}
