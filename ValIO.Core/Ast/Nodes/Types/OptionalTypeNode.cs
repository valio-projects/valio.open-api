using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Types;

/// <summary>
/// Узел optional типа (для TypeScript)
/// </summary>
public sealed class OptionalTypeNode : TypeNode
{
    /// <summary>
    /// Внутренний тип
    /// </summary>
    public TypeNode InnerType { get; }
    
    public override AstNodeKind Kind => AstNodeKind.OptionalType;
    
    public override IReadOnlyList<AstNode> Children => new[] { InnerType };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public OptionalTypeNode(
        TypeNode innerType,
        bool isNullable = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(isNullable, isOptional: true, metadata)
    {
        InnerType = innerType ?? throw new ArgumentNullException(nameof(innerType));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitOptionalType(this);
    }
    
    public override AstNode Clone()
    {
        return new OptionalTypeNode(
            (TypeNode)InnerType.Clone(),
            IsNullable,
            Metadata.ToImmutableDictionary());
    }
}
