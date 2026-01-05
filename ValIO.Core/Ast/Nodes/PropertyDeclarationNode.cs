using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел объявления свойства
/// </summary>
public sealed class PropertyDeclarationNode : DeclarationNode
{
    /// <summary>
    /// Тип свойства
    /// </summary>
    public TypeNode Type { get; }
    
    /// <summary>
    /// Значение по умолчанию
    /// </summary>
    public ExpressionNode? DefaultValue { get; }
    
    /// <summary>
    /// Является ли свойство обязательным
    /// </summary>
    public bool IsRequired { get; }
    
    /// <summary>
    /// Тип аксессора (get/set/init для C#)
    /// </summary>
    public PropertyAccessorKind Accessor { get; }
    
    public override AstNodeKind Kind => AstNodeKind.PropertyDeclaration;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, Type };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            if (DefaultValue != null)
                children.Add(DefaultValue);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public PropertyDeclarationNode(
        IdentifierNode name,
        TypeNode type,
        bool isRequired = false,
        PropertyAccessorKind accessor = PropertyAccessorKind.GetSet,
        ExpressionNode? defaultValue = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes, metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsRequired = isRequired;
        Accessor = accessor;
        DefaultValue = defaultValue;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitPropertyDeclaration(this);
    }
    
    public override AstNode Clone()
    {
        return new PropertyDeclarationNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            IsRequired,
            Accessor,
            DefaultValue != null ? (ExpressionNode)DefaultValue.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
