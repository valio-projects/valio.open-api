using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

/// <summary>
/// Arrow function expression: (params) => expression или (params) => { statements }
/// </summary>
public sealed class ArrowFunctionExpressionNode : ExpressionNode
{
    public IReadOnlyList<ParameterDeclarationNode> Parameters { get; }
    public ExpressionNode? BodyExpression { get; }
    public BlockNode? BodyBlock { get; }
    public bool IsAsync { get; }

    /// <summary>
    /// Возвращаемый тип стрелочной функции: <c>(p) =&gt; ReturnType</c> (TypeScript-only).
    /// </summary>
    public TypeNode? ReturnType { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var list = new List<AstNode>();
            list.AddRange(Parameters);
            if (BodyExpression != null)
                list.Add(BodyExpression);
            if (BodyBlock != null)
                list.Add(BodyBlock);
            return list;
        }
    }

    public ArrowFunctionExpressionNode(
        IReadOnlyList<ParameterDeclarationNode> parameters,
        ExpressionNode bodyExpression,
        bool isAsync = false,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        TypeNode? returnType = null)
        : base(type, metadata)
    {
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<ParameterDeclarationNode>.Empty;
        BodyExpression = bodyExpression;
        BodyBlock = null;
        IsAsync = isAsync;
        ReturnType = returnType;
    }

    public ArrowFunctionExpressionNode(
        IReadOnlyList<ParameterDeclarationNode> parameters,
        BlockNode bodyBlock,
        bool isAsync = false,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        TypeNode? returnType = null)
        : base(type, metadata)
    {
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<ParameterDeclarationNode>.Empty;
        BodyExpression = null;
        BodyBlock = bodyBlock;
        IsAsync = isAsync;
        ReturnType = returnType;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> tsVisitor)
            return tsVisitor.VisitArrowFunctionExpression(this);
        return visitor.DefaultVisit(this);
    }

    public override AstNode Clone()
    {
        if (BodyExpression != null)
        {
            return new ArrowFunctionExpressionNode(
                Parameters.Select(p => (ParameterDeclarationNode)p.Clone()).ToImmutableList(),
                (ExpressionNode)BodyExpression.Clone(),
                IsAsync,
                Type != null ? (TypeNode)Type.Clone() : null,
                Metadata.ToImmutableDictionary(),
                ReturnType != null ? (TypeNode)ReturnType.Clone() : null);
        }
        return new ArrowFunctionExpressionNode(
            Parameters.Select(p => (ParameterDeclarationNode)p.Clone()).ToImmutableList(),
            BodyBlock != null ? (BlockNode)BodyBlock.Clone() : null!,
            IsAsync,
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null);
    }
}
