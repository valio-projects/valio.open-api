using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Nodes;

/// <summary>Runs a branch when an F# option has a value.</summary>
/// <param name="option">The optional value to inspect.</param>
/// <param name="presentName">The name of the Some value.</param>
/// <param name="present">The statement for the Some case.</param>
public sealed class FSharpMatchOptionNode(ExpressionNode option, IdentifierNode presentName, StatementNode present) : StatementNode
{
    /// <summary>Gets the optional value to inspect.</summary>
    /// <value>The optional value to inspect.</value>
    public ExpressionNode Option { get; } = option;

    /// <summary>Gets the name of the Some value.</summary>
    /// <value>The name of the Some value.</value>
    public IdentifierNode PresentName { get; } = presentName;

    /// <summary>Gets the statement for the Some case.</summary>
    /// <value>The statement for the Some case.</value>
    public StatementNode Present { get; } = present;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Option, PresentName, Present];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IFSharpAstVisitor<TResult> fsharp ? fsharp.VisitFSharpMatchOption(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new FSharpMatchOptionNode((ExpressionNode)Option.Clone(), (IdentifierNode)PresentName.Clone(), (StatementNode)Present.Clone());
}
