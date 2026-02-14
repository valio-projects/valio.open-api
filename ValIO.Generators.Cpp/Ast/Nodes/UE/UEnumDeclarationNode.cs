using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE;

/// <summary>
/// Значение UE enum
/// </summary>
public sealed class UEnumValueNode : AstNode
{
    /// <summary>
    /// Имя значения enum
    /// </summary>
    public IdentifierNode Name { get; }

    /// <summary>
    /// Числовое значение (может быть null для автоинкремента)
    /// </summary>
    public ExpressionNode? Value { get; }

    /// <summary>
    /// Мета-комментарий (DisplayName и т.д.)
    /// </summary>
    public string? DisplayName { get; }

    public override AstNodeKind Kind => AstNodeKind.EnumMemberDeclaration;
    public override IReadOnlyList<AstNode> Children => Value != null ? new AstNode[] { Name, Value } : new AstNode[] { Name };

    public UEnumValueNode(
        IdentifierNode name,
        ExpressionNode? value = null,
        string? displayName = null,
        IReadOnlyDictionary<string, object>? metadata = null) : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;
        DisplayName = displayName;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) => visitor.VisitEnumMemberDeclaration(
        new EnumMemberDeclarationNode(Name, Value, metadata: Metadata.ToImmutableDictionary()));

    public override AstNode Clone() => new UEnumValueNode(
        (IdentifierNode)Name.Clone(), Value != null ? (ExpressionNode)Value.Clone() : null, DisplayName,
        Metadata.ToImmutableDictionary());
}

/// <summary>
/// Узел объявления UENUM в Unreal Engine.
/// Генерирует: UENUM(BlueprintType) enum class E{Name} : uint8 { ... };
/// </summary>
public sealed class UEnumDeclarationNode : DeclarationNode
{
    /// <summary>
    /// Базовый тип enum (uint8 по умолчанию для Blueprints)
    /// </summary>
    public TypeNode UnderlyingType { get; }

    /// <summary>
    /// Значения enum
    /// </summary>
    public IReadOnlyList<UEnumValueNode> Values { get; }

    public override AstNodeKind Kind => AstNodeKind.UEnum;

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, UnderlyingType };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(Values);
            return children;
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public UEnumDeclarationNode(
        IdentifierNode name,
        TypeNode underlyingType,
        IReadOnlyList<UEnumValueNode>? values = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, metadata: metadata)
    {
        UnderlyingType = underlyingType ?? throw new ArgumentNullException(nameof(underlyingType));
        Values = values?.ToImmutableList() ?? ImmutableList<UEnumValueNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitUEnum(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit UEnumDeclarationNode");
    }

    public override AstNode Clone()
    {
        return new UEnumDeclarationNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)UnderlyingType.Clone(),
            Values.Select(v => (UEnumValueNode)v.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        return $"UENUM {Name.Name} : {UnderlyingType} [{Values.Count} values]";
    }
}
