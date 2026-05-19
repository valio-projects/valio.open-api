using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Реализация метода Complete Async Action.
/// Тело формируется через AST (BlockNode).
/// </summary>
public sealed class CppAsyncCompleteImplementationNode : AstNode
{
    public IdentifierNode ClassName { get; }
    public IdentifierNode MethodName { get; }
    public TypeNode? ReturnType { get; }
    public IReadOnlyList<CppParameterNode> Parameters { get; }
    public BlockNode Body { get; }


    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var list = new List<AstNode> { ClassName, MethodName };
            if (ReturnType != null) list.Add(ReturnType);
            list.AddRange(Parameters);
            list.Add(Body);
            return list;
        }
    }

    public CppAsyncCompleteImplementationNode(
        IdentifierNode className,
        IdentifierNode methodName,
        TypeNode? returnType,
        IReadOnlyList<CppParameterNode> parameters,
        BlockNode body)
        : base(null)
    {
        ClassName = className ?? throw new ArgumentNullException(nameof(className));
        MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        ReturnType = returnType;
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<CppParameterNode>.Empty;
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppAsyncCompleteImplementation(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor to visit CppAsyncCompleteImplementationNode");
    }

    public override AstNode Clone()
    {
        return new CppAsyncCompleteImplementationNode(
            (IdentifierNode)ClassName.Clone(),
            (IdentifierNode)MethodName.Clone(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            Parameters.Select(p => (CppParameterNode)p.Clone()).ToImmutableList(),
            (BlockNode)Body.Clone());
    }
}
