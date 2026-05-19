using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Реализация метода Path builder: FString X::PathY(...) с Printf в промежуточную Url переменную.
/// Тело формируется через AST (BlockNode).
/// </summary>
public sealed class CppPathBuilderImplementationNode : AstNode
{
    public IdentifierNode ClassName { get; }
    public IdentifierNode MethodName { get; }
    public IReadOnlyList<CppParameterNode> Parameters { get; }
    public BlockNode Body { get; }


    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var list = new List<AstNode> { ClassName, MethodName };
            list.AddRange(Parameters);
            list.Add(Body);
            return list;
        }
    }

    public CppPathBuilderImplementationNode(
        IdentifierNode className,
        IdentifierNode methodName,
        IReadOnlyList<CppParameterNode> parameters,
        BlockNode body)
        : base(null)
    {
        ClassName = className ?? throw new ArgumentNullException(nameof(className));
        MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<CppParameterNode>.Empty;
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppPathBuilderImplementation(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor to visit CppPathBuilderImplementationNode");
    }

    public override AstNode Clone()
    {
        return new CppPathBuilderImplementationNode(
            (IdentifierNode)ClassName.Clone(),
            (IdentifierNode)MethodName.Clone(),
            Parameters.Select(p => (CppParameterNode)p.Clone()).ToImmutableList(),
            (BlockNode)Body.Clone());
    }
}
