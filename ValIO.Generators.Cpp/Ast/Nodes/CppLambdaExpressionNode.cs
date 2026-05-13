using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// C++ lambda expression: [captures](params) { body }
/// </summary>
public sealed class CppLambdaExpressionNode : ExpressionNode
{
    public IReadOnlyList<string> Captures { get; }
    public IReadOnlyList<CppParameterNode> Parameters { get; }
    public BlockNode Body { get; }


    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var list = new List<AstNode>();
            list.AddRange(Parameters);
            list.Add(Body);
            return list;
        }
    }

    public CppLambdaExpressionNode(
        IReadOnlyList<string> captures,
        IReadOnlyList<CppParameterNode> parameters,
        BlockNode body,
        TypeNode? type = null)
        : base(type)
    {
        Captures = captures?.ToImmutableList() ?? ImmutableList<string>.Empty;
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<CppParameterNode>.Empty;
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppLambdaExpression(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor to visit CppLambdaExpressionNode");
    }

    public override AstNode Clone()
    {
        return new CppLambdaExpressionNode(
            Captures.ToImmutableList(),
            Parameters.Select(p => (CppParameterNode)p.Clone()).ToImmutableList(),
            (BlockNode)Body.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null);
    }
}
