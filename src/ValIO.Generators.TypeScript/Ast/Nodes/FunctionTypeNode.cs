using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

/// <summary>Represents an ordered call signature used as a TypeScript type.</summary>
/// <param name="parameters">The positional parameters of the callback.</param>
/// <param name="result">The type returned by the callback.</param>
public sealed class FunctionTypeNode(IReadOnlyList<ParameterDeclarationNode> parameters, TypeNode result) : TypeNode
{
    /// <summary>Gets the positional parameters of the callback.</summary>
    /// <value>The parameters in declaration order.</value>
    public IReadOnlyList<ParameterDeclarationNode> Parameters { get; } = parameters.ToArray();

    /// <summary>Gets the type returned by the callback.</summary>
    /// <value>The callback's result type.</value>
    public TypeNode Result { get; } = result;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [.. Parameters, Result];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ITypeScriptAstVisitor<TResult> typeScript
            ? typeScript.VisitFunctionType(this)
            : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new FunctionTypeNode(
        Parameters.Select(parameter => (ParameterDeclarationNode)parameter.Clone()).ToArray(), (TypeNode)Result.Clone());
}
