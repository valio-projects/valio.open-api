using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE;

/// <summary>
/// Узел объявления USTRUCT в Unreal Engine.
/// Генерирует: USTRUCT(BlueprintType) struct F{Name} { GENERATED_BODY() ... };
/// </summary>
public sealed class UStructDeclarationNode : TypeDeclarationNode
{
    /// <summary>
    /// Спецификаторы USTRUCT
    /// </summary>
    public UStructSpecifiers Specifiers { get; }

    /// <summary>
    /// Поля структуры с UPROPERTY
    /// </summary>
    public IReadOnlyList<UPropertyFieldNode> Fields { get; }

    /// <summary>
    /// Дополнительные (не-UPROPERTY) методы/поля (например IsSuccess())
    /// </summary>
    public IReadOnlyList<CppFreeFunctionNode> InlineMethods { get; }

    public override AstNodeKind Kind => AstNodeKind.UStruct;

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(Fields);
            children.AddRange(InlineMethods);
            return children;
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public UStructDeclarationNode(
        IdentifierNode name,
        UStructSpecifiers? specifiers = null,
        IReadOnlyList<UPropertyFieldNode>? fields = null,
        IReadOnlyList<CppFreeFunctionNode>? inlineMethods = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation: documentation, metadata: metadata)
    {
        Specifiers = specifiers ?? new UStructSpecifiers();
        Fields = fields?.ToImmutableList() ?? ImmutableList<UPropertyFieldNode>.Empty;
        InlineMethods = inlineMethods?.ToImmutableList() ?? ImmutableList<CppFreeFunctionNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitUStruct(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit UStructDeclarationNode");
    }

    public override AstNode Clone()
    {
        return new UStructDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Specifiers,
            Fields.Select(f => (UPropertyFieldNode)f.Clone()).ToImmutableList(),
            InlineMethods.Select(m => (CppFreeFunctionNode)m.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        return $"USTRUCT {Name.Name} [{Fields.Count} fields, {InlineMethods.Count} methods]";
    }
}
