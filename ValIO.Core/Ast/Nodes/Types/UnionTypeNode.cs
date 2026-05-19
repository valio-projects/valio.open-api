using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Types;

/// <summary>
/// Узел union типа (A | B | C)
/// </summary>
public sealed class UnionTypeNode : TypeNode
{
    /// <summary>
    /// Типы в union
    /// </summary>
    public IReadOnlyList<TypeNode> Types { get; }
    
    
    public override IReadOnlyList<AstNode> Children => Types.Cast<AstNode>().ToImmutableList();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public UnionTypeNode(
        IReadOnlyList<TypeNode> types,
        bool isNullable = false,
        bool isOptional = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(isNullable, isOptional, metadata)
    {
        if (types == null || types.Count == 0)
            throw new ArgumentException("Union type must have at least one type", nameof(types));
        
        Types = types.ToImmutableList();
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitUnionType(this);
    }
    
    public override AstNode Clone()
    {
        return new UnionTypeNode(
            Types.Select(t => (TypeNode)t.Clone()).ToImmutableList(),
            IsNullable,
            IsOptional,
            Metadata.ToImmutableDictionary());
    }
}
