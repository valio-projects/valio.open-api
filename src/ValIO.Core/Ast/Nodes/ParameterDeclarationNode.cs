using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел объявления параметра
/// </summary>
public sealed class ParameterDeclarationNode : DeclarationNode
{
    /// <summary>
    /// Тип параметра. Null означает, что аннотация типа должна быть опущена
    /// в выводе (например, для arrow‑callback с TS‑инференцией: <c>(This) => ...</c>).
    /// </summary>
    public TypeNode? Type { get; }
    
    /// <summary>
    /// Значение по умолчанию
    /// </summary>
    public ExpressionNode? DefaultValue { get; }
    
    /// <summary>
    /// Является ли параметр обязательным
    /// </summary>
    public bool IsRequired { get; }
    
    /// <summary>
    /// Тип параметра (обычный/ref/out/in для C#)
    /// </summary>
    public ParameterKind ParameterKind { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Type != null)
                children.Add(Type);
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
    public ParameterDeclarationNode(
        IdentifierNode name,
        TypeNode? type,
        bool isRequired = true,
        ParameterKind parameterKind = ParameterKind.Normal,
        ExpressionNode? defaultValue = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes, metadata)
    {
        Type = type;
        IsRequired = isRequired;
        ParameterKind = parameterKind;
        DefaultValue = defaultValue;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitParameterDeclaration(this);
    }

    public override AstNode Clone()
    {
        return new ParameterDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null,
            IsRequired,
            ParameterKind,
            DefaultValue != null ? (ExpressionNode)DefaultValue.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        var requiredStr = IsRequired ? "" : "?";
        var defaultValueStr = DefaultValue != null ? $" = {DefaultValue}" : "";
        var typeStr = Type?.ToString() ?? "";
        return $"{typeStr}{requiredStr} {Name.Name}{defaultValueStr}";
    }
}
