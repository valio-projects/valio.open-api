using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Types;

/// <summary>
/// Узел nullable типа
/// </summary>
public sealed class NullableTypeNode : TypeNode
{
    /// <summary>
    /// Внутренний тип
    /// </summary>
    public TypeNode InnerType { get; }
    
    public override AstNodeKind Kind => AstNodeKind.NullableType;
    
    public override IReadOnlyList<AstNode> Children => new[] { InnerType };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public NullableTypeNode(
        TypeNode innerType,
        bool isOptional = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(isNullable: true, isOptional, metadata)
    {
        InnerType = innerType ?? throw new ArgumentNullException(nameof(innerType));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitNullableType(this);
    }
    
    public override AstNode Clone()
    {
        return new NullableTypeNode(
            (TypeNode)InnerType.Clone(),
            IsOptional,
            Metadata.ToImmutableDictionary());
    }
    
    public override string ToString()
    {
        return $"{InnerType}?";
    }
}
