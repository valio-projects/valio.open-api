using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE;

/// <summary>
/// Узел объявления UCLASS в Unreal Engine.
/// Генерирует: UCLASS(...) class MODULE_API UName : public UBase { GENERATED_BODY() ... };
/// Используется для Blueprint async actions, Function Libraries и т.д.
/// </summary>
public sealed class UClassDeclarationNode : TypeDeclarationNode
{
    /// <summary>
    /// Спецификаторы UCLASS
    /// </summary>
    public UClassSpecifiers Specifiers { get; }

    /// <summary>
    /// Макрос API модуля (например WIREFRAME_API, MYPROJECT_API).
    /// Вставляется между class и именем класса.
    /// </summary>
    public string? ApiMacro { get; }

    /// <summary>
    /// Public-секция: свойства, методы, поля
    /// </summary>
    public IReadOnlyList<AstNode> PublicMembers { get; }

    /// <summary>
    /// Protected-секция
    /// </summary>
    public IReadOnlyList<AstNode> ProtectedMembers { get; }

    public override AstNodeKind Kind => AstNodeKind.UClass;

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(BaseTypes);
            children.AddRange(PublicMembers);
            children.AddRange(ProtectedMembers);
            return children;
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public UClassDeclarationNode(
        IdentifierNode name,
        UClassSpecifiers? specifiers = null,
        string? apiMacro = null,
        IReadOnlyList<TypeNode>? baseTypes = null,
        IReadOnlyList<AstNode>? publicMembers = null,
        IReadOnlyList<AstNode>? protectedMembers = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, baseTypes: baseTypes, documentation: documentation, metadata: metadata)
    {
        Specifiers = specifiers ?? new UClassSpecifiers();
        ApiMacro = apiMacro;
        PublicMembers = publicMembers?.ToImmutableList() ?? ImmutableList<AstNode>.Empty;
        ProtectedMembers = protectedMembers?.ToImmutableList() ?? ImmutableList<AstNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitUClass(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit UClassDeclarationNode");
    }

    public override AstNode Clone()
    {
        return new UClassDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Specifiers,
            ApiMacro,
            BaseTypes.Select(t => (TypeNode)t.Clone()).ToImmutableList(),
            PublicMembers.Select(m => m.Clone()).ToImmutableList(),
            ProtectedMembers.Select(m => m.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        var basesStr = BaseTypes.Count > 0 ? $" : {string.Join(", ", BaseTypes)}" : "";
        return $"UCLASS {Name.Name}{basesStr} [pub:{PublicMembers.Count}, prot:{ProtectedMembers.Count}]";
    }
}
