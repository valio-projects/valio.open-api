using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE;

/// <summary>
/// Узел поля с UPROPERTY макросом в Unreal Engine.
/// Пример: UPROPERTY(BlueprintReadWrite, Category = "Panoramas") FString name;
/// </summary>
public sealed class UPropertyFieldNode : DeclarationNode
{
    /// <summary>
    /// Тип поля
    /// </summary>
    public TypeNode Type { get; }

    /// <summary>
    /// Значение по умолчанию (может быть null)
    /// </summary>
    public ExpressionNode? DefaultValue { get; }

    /// <summary>
    /// Спецификаторы UPROPERTY
    /// </summary>
    public UPropertySpecifiers Specifiers { get; }

    public override AstNodeKind Kind => AstNodeKind.UPropertyField;

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, Type };
            if (DefaultValue != null) children.Add(DefaultValue);
            if (Documentation != null) children.Add(Documentation);
            return children;
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public UPropertyFieldNode(
        IdentifierNode name,
        TypeNode type,
        UPropertySpecifiers? specifiers = null,
        ExpressionNode? defaultValue = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, metadata: metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Specifiers = specifiers ?? new UPropertySpecifiers();
        DefaultValue = defaultValue;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitUPropertyField(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit UPropertyFieldNode");
    }

    public override AstNode Clone()
    {
        return new UPropertyFieldNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            Specifiers,
            DefaultValue != null ? (ExpressionNode)DefaultValue.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        var defaultStr = DefaultValue != null ? $" = {DefaultValue}" : "";
        return $"UPROPERTY {Type} {Name.Name}{defaultStr}";
    }
}
