using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Expressions;

/// <summary>
/// Узел выражения object literal (TypeScript)
/// </summary>
public sealed class ObjectLiteralExpressionNode : ExpressionNode
{
    /// <summary>
    /// Свойства объекта
    /// </summary>
    public IReadOnlyList<PropertyAssignmentNode> Properties { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ObjectLiteralExpression;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = Properties.Cast<AstNode>().ToList();
            if (Type != null)
                children.Add(Type);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ObjectLiteralExpressionNode(
        IReadOnlyList<PropertyAssignmentNode>? properties = null,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        Properties = properties?.ToImmutableList() ?? ImmutableList<PropertyAssignmentNode>.Empty;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitObjectLiteralExpression(this);
        }
        throw new NotSupportedException($"Visitor must implement ITypeScriptAstVisitor<TResult> to visit ObjectLiteralExpressionNode");
    }
    
    public override AstNode Clone()
    {
        return new ObjectLiteralExpressionNode(
            Properties.Select(p => (PropertyAssignmentNode)p.Clone()).ToImmutableList(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
