using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Visitors;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

/// <summary>
/// Узел опций для useMutation
/// </summary>
public sealed class MutationOptionsNode : ObjectLiteralExpressionNode
{
    /// <summary>
    /// OnSuccess callback
    /// </summary>
    public ExpressionNode? OnSuccess { get; }
    
    /// <summary>
    /// OnError callback
    /// </summary>
    public ExpressionNode? OnError { get; }
    
    /// <summary>
    /// OnSettled callback
    /// </summary>
    public ExpressionNode? OnSettled { get; }
    
    public override AstNodeKind Kind => AstNodeKind.MutationOptions;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (OnSuccess != null) children.Add(OnSuccess);
            if (OnError != null) children.Add(OnError);
            if (OnSettled != null) children.Add(OnSettled);
            children.AddRange(Properties);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public MutationOptionsNode(
        ExpressionNode? onSuccess = null,
        ExpressionNode? onError = null,
        ExpressionNode? onSettled = null,
        IReadOnlyList<PropertyAssignmentNode>? properties = null,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(properties ?? ImmutableList<PropertyAssignmentNode>.Empty, type, metadata)
    {
        OnSuccess = onSuccess;
        OnError = onError;
        OnSettled = onSettled;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITanStackQueryAstVisitor<TResult> queryVisitor)
        {
            return queryVisitor.VisitMutationOptions(this);
        }
        return visitor.VisitObjectLiteralExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new MutationOptionsNode(
            OnSuccess != null ? (ExpressionNode)OnSuccess.Clone() : null,
            OnError != null ? (ExpressionNode)OnError.Clone() : null,
            OnSettled != null ? (ExpressionNode)OnSettled.Clone() : null,
            Properties.Select(p => (PropertyAssignmentNode)p.Clone()).ToImmutableList(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
