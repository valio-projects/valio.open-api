using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Nodes;

/// <summary>Represents application of an F# function to a single argument.</summary>
/// <param name="function">The applied function.</param>
/// <param name="argument">The argument of the application.</param>
public sealed class FSharpApplicationNode(ExpressionNode function, ExpressionNode argument) : ExpressionNode
{
    /// <summary>Gets the applied function.</summary>
    /// <value>The applied function.</value>
    public ExpressionNode Function { get; } = function;

    /// <summary>Gets the argument of the application.</summary>
    /// <value>The argument of the application.</value>
    public ExpressionNode Argument { get; } = argument;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Function, Argument];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IFSharpAstVisitor<TResult> fsharp ? fsharp.VisitFSharpApplication(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new FSharpApplicationNode((ExpressionNode)Function.Clone(), (ExpressionNode)Argument.Clone());
}
