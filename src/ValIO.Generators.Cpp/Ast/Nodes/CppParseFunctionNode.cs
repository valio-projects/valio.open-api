using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Parse-функция в анонимном namespace: ResultType ParseXxxResult(const FValIOResponse&amp; Response).
/// Тело формируется через AST (BlockNode).
/// </summary>
public sealed class CppParseFunctionNode : AstNode
{
    public IdentifierNode Name { get; }
    public TypeNode ReturnType { get; }
    public IReadOnlyList<CppParameterNode> Parameters { get; }
    public BlockNode Body { get; }


    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var list = new List<AstNode> { Name, ReturnType };
            list.AddRange(Parameters);
            list.Add(Body);
            return list;
        }
    }

    public CppParseFunctionNode(
        IdentifierNode name,
        TypeNode returnType,
        IReadOnlyList<CppParameterNode> parameters,
        BlockNode body)
        : base(null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<CppParameterNode>.Empty;
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppParseFunction(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor to visit CppParseFunctionNode");
    }

    public override AstNode Clone()
    {
        return new CppParseFunctionNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)ReturnType.Clone(),
            Parameters.Select(p => (CppParameterNode)p.Clone()).ToImmutableList(),
            (BlockNode)Body.Clone());
    }
}
