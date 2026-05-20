using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Реализация метода Activate Async Action.
/// Тело формируется через AST (BlockNode).
/// </summary>
public sealed class CppAsyncActivateImplementationNode : AstNode
{
    public IdentifierNode ClassName { get; }
    public BlockNode Body { get; }


    public override IReadOnlyList<AstNode> Children => new List<AstNode> { ClassName, Body };

    public CppAsyncActivateImplementationNode(
        IdentifierNode className,
        BlockNode body)
        : base(null)
    {
        ClassName = className ?? throw new ArgumentNullException(nameof(className));
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppAsyncActivateImplementation(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor to visit CppAsyncActivateImplementationNode");
    }

    public override AstNode Clone()
    {
        return new CppAsyncActivateImplementationNode(
            (IdentifierNode)ClassName.Clone(),
            (BlockNode)Body.Clone());
    }
}
