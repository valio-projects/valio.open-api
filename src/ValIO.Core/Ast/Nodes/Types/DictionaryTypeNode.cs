using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Types;

/// <summary>
/// Узел типа словаря (Dictionary&lt;K, V&gt; или Record&lt;K, V&gt;)
/// </summary>
public sealed class DictionaryTypeNode : TypeNode
{
    /// <summary>
    /// Тип ключа
    /// </summary>
    public TypeNode KeyType { get; }
    
    /// <summary>
    /// Тип значения
    /// </summary>
    public TypeNode ValueType { get; }
    
    
    public override IReadOnlyList<AstNode> Children => new[] { KeyType, ValueType };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public DictionaryTypeNode(
        TypeNode keyType,
        TypeNode valueType,
        bool isNullable = false,
        bool isOptional = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(isNullable, isOptional, metadata)
    {
        KeyType = keyType ?? throw new ArgumentNullException(nameof(keyType));
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitDictionaryType(this);
    }
    
    public override AstNode Clone()
    {
        return new DictionaryTypeNode(
            (TypeNode)KeyType.Clone(),
            (TypeNode)ValueType.Clone(),
            IsNullable,
            IsOptional,
            Metadata.ToImmutableDictionary());
    }
}
