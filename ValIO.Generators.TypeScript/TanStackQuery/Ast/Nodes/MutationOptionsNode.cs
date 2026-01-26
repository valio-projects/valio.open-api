using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Ast.Visitors;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Visitors;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

/// <summary>
/// Узел опций для useMutation
/// </summary>
public sealed class MutationOptionsNode : ExpressionNode
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
    
    /// <summary>
    /// Дополнительные свойства (object literal properties)
    /// </summary>
    public IReadOnlyList<PropertyAssignmentNode> Properties { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ObjectLiteralExpression; // Используем существующий kind
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (OnSuccess != null) children.Add(OnSuccess);
            if (OnError != null) children.Add(OnError);
            if (OnSettled != null) children.Add(OnSettled);
            children.AddRange(Properties);
            if (Type != null)
                children.Add(Type);
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
        : base(type, metadata)
    {
        OnSuccess = onSuccess;
        OnError = onError;
        OnSettled = onSettled;
        Properties = properties?.ToImmutableList() ?? ImmutableList<PropertyAssignmentNode>.Empty;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITanStackQueryAstVisitor<TResult> queryVisitor)
        {
            return queryVisitor.VisitMutationOptions(this);
        }
        // Fallback на object literal expression через ITypeScriptAstVisitor
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            var objectLiteral = new ObjectLiteralExpressionNode(Properties, Type, Metadata);
            return typescriptVisitor.VisitObjectLiteralExpression(objectLiteral);
        }
        return visitor.VisitExpression(this);
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
