using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Types;

/// <summary>
/// Узел простого типа (не generic)
/// </summary>
public sealed class SimpleTypeNode : TypeNode
{
    /// <summary>
    /// Имя типа
    /// </summary>
    public IdentifierNode Name { get; }
    
    /// <summary>
    /// Является ли тип примитивным
    /// </summary>
    public bool IsPrimitive { get; }
    
    public override AstNodeKind Kind => AstNodeKind.SimpleType;
    
    public override IReadOnlyList<AstNode> Children => new[] { Name };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public SimpleTypeNode(
        IdentifierNode name,
        bool isPrimitive = false,
        bool isNullable = false,
        bool isOptional = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(isNullable, isOptional, metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IsPrimitive = isPrimitive;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitSimpleType(this);
    }
    
    public override AstNode Clone()
    {
        return new SimpleTypeNode(
            (IdentifierNode)Name.Clone(),
            IsPrimitive,
            IsNullable,
            IsOptional,
            Metadata.ToImmutableDictionary());
    }
    
    public override string ToString()
    {
        var nullableStr = IsNullable ? "?" : "";
        return $"{Name.Name}{nullableStr}";
    }
}
