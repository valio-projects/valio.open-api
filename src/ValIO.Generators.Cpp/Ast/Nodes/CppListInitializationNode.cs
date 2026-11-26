using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>Represents a braced list initializer, including an empty value initializer.</summary>
/// <param name="values">The values in initializer order.</param>
public sealed class CppListInitializationNode(IReadOnlyList<ExpressionNode> values) : ExpressionNode
{
    /// <summary>Gets the values in initializer order.</summary>
    public IReadOnlyList<ExpressionNode> Values { get; } = values.ToArray();

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children => Values;

    /// <inheritdoc />
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ICppAstVisitor<TResult> cppVisitor
            ? cppVisitor.VisitCppListInitialization(this)
            : throw new NotSupportedException("A C++ visitor is required for list initialization.");

    /// <inheritdoc />
    public override AstNode Clone() => new CppListInitializationNode(Values.Select(value => (ExpressionNode)value.Clone()).ToArray());
}
