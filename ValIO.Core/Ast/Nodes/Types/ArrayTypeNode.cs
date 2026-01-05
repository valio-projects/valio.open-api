using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Types;

/// <summary>
/// Узел типа массива
/// </summary>
public sealed class ArrayTypeNode : TypeNode
{
    /// <summary>
    /// Тип элементов
    /// </summary>
    public TypeNode ElementType { get; }
    
    /// <summary>
    /// Является ли массив readonly (для TypeScript)
    /// </summary>
    public bool IsReadOnly { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ArrayType;
    
    public override IReadOnlyList<AstNode> Children => new[] { ElementType };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ArrayTypeNode(
        TypeNode elementType,
        bool isReadOnly = false,
        bool isNullable = false,
        bool isOptional = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(isNullable, isOptional, metadata)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
        IsReadOnly = isReadOnly;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitArrayType(this);
    }
    
    public override AstNode Clone()
    {
        return new ArrayTypeNode(
            (TypeNode)ElementType.Clone(),
            IsReadOnly,
            IsNullable,
            IsOptional,
            Metadata.ToImmutableDictionary());
    }
}
