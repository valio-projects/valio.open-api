using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>References a C++ type in an expression such as value initialization.</summary>
/// <param name="referencedType">The type named by the expression.</param>
public sealed class CppTypeExpressionNode(TypeNode referencedType) : ExpressionNode
{
    /// <summary>Gets the type named by the expression.</summary>
    public TypeNode ReferencedType { get; } = referencedType;

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children => [ReferencedType];

    /// <inheritdoc />
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ICppAstVisitor<TResult> cppVisitor
            ? cppVisitor.VisitCppTypeExpression(this)
            : throw new NotSupportedException("A C++ visitor is required for a type expression.");

    /// <inheritdoc />
    public override AstNode Clone() => new CppTypeExpressionNode((TypeNode)ReferencedType.Clone());
}
