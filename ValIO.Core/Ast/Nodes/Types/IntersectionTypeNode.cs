using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Types;

/// <summary>
/// Узел intersection типа (A & B & C)
/// </summary>
public sealed class IntersectionTypeNode : TypeNode
{
    /// <summary>
    /// Типы в intersection
    /// </summary>
    public IReadOnlyList<TypeNode> Types { get; }
    
    public override AstNodeKind Kind => AstNodeKind.IntersectionType;
    
    public override IReadOnlyList<AstNode> Children => Types.Cast<AstNode>().ToImmutableList();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public IntersectionTypeNode(
        IReadOnlyList<TypeNode> types,
        bool isNullable = false,
        bool isOptional = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(isNullable, isOptional, metadata)
    {
        if (types == null || types.Count == 0)
            throw new ArgumentException("Intersection type must have at least one type", nameof(types));
        
        Types = types.ToImmutableList();
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitIntersectionType(this);
    }
    
    public override AstNode Clone()
    {
        return new IntersectionTypeNode(
            Types.Select(t => (TypeNode)t.Clone()).ToImmutableList(),
            IsNullable,
            IsOptional,
            Metadata.ToImmutableDictionary());
    }
}
