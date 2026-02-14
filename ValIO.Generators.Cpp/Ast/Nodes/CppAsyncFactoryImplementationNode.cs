using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Реализация статического factory метода Async Action.
/// Тело формируется через AST (BlockNode).
/// </summary>
public sealed class CppAsyncFactoryImplementationNode : AstNode
{
    public IdentifierNode ClassName { get; }
    public IdentifierNode MethodName { get; }
    public TypeNode ReturnType { get; }
    public IReadOnlyList<CppParameterNode> Parameters { get; }
    public BlockNode Body { get; }

    public override AstNodeKind Kind => AstNodeKind.CppAsyncFactoryImplementation;

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var list = new List<AstNode> { ClassName, MethodName, ReturnType };
            list.AddRange(Parameters);
            list.Add(Body);
            return list;
        }
    }

    public CppAsyncFactoryImplementationNode(
        IdentifierNode className,
        IdentifierNode methodName,
        TypeNode returnType,
        IReadOnlyList<CppParameterNode> parameters,
        BlockNode body)
        : base(null)
    {
        ClassName = className ?? throw new ArgumentNullException(nameof(className));
        MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<CppParameterNode>.Empty;
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppAsyncFactoryImplementation(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor to visit CppAsyncFactoryImplementationNode");
    }

    public override AstNode Clone()
    {
        return new CppAsyncFactoryImplementationNode(
            (IdentifierNode)ClassName.Clone(),
            (IdentifierNode)MethodName.Clone(),
            (TypeNode)ReturnType.Clone(),
            Parameters.Select(p => (CppParameterNode)p.Clone()).ToImmutableList(),
            (BlockNode)Body.Clone());
    }
}
