using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Core.IR.Constraints;

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
    
    /// <summary>
    /// Ограничения свойства (minLength, maxLength, pattern и т.д.)
    /// </summary>
    public IrPropertyConstraints? Constraints { get; }
    
    /// <summary>
    /// Пример значения для JSDoc @example
    /// </summary>
    public string? Example { get; }
    
    /// <summary>
    /// TODO-метка для JSDoc @todo
    /// </summary>
    public string? Todo { get; }
    
    /// <summary>
    /// Свойство только для чтения
    /// </summary>
    public bool IsReadOnly { get; }
    
    /// <summary>
    /// Свойство устарело (deprecated)
    /// </summary>
    public bool IsDeprecated { get; }
    
    /// <summary>
    /// Значение является enum
    /// </summary>
    public bool IsEnum { get; }
    
    /// <summary>
    /// Значение является константой
    /// </summary>
    public bool IsConstant { get; }
    
    
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
        IReadOnlyDictionary<string, object>? metadata = null,
        IrPropertyConstraints? constraints = null,
        string? example = null,
        string? todo = null,
        bool isReadOnly = false,
        bool isDeprecated = false,
        bool isEnum = false,
        bool isConstant = false)
        : base(name, documentation, attributes, metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsRequired = isRequired;
        Accessor = accessor;
        DefaultValue = defaultValue;
        Constraints = constraints;
        Example = example;
        Todo = todo;
        IsReadOnly = isReadOnly;
        IsDeprecated = isDeprecated;
        IsEnum = isEnum;
        IsConstant = isConstant;
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
            Metadata.ToImmutableDictionary(),
            Constraints,
            Example,
            Todo,
            IsReadOnly,
            IsDeprecated,
            IsEnum,
            IsConstant);
    }
    
    public override string ToString()
    {
        var requiredStr = IsRequired ? "required " : "";
        return $"{Type} {Name.Name} {{ {Accessor} }} [{requiredStr}]";
    }
}
