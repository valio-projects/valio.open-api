using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел объявления интерфейса (общий для C# и TypeScript)
/// </summary>
public sealed class InterfaceDeclarationNode : TypeDeclarationNode
{
    public override AstNodeKind Kind => AstNodeKind.InterfaceDeclaration;
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public InterfaceDeclarationNode(
        IdentifierNode name,
        AccessModifier accessModifier = AccessModifier.Public,
        IReadOnlyList<TypeNode>? baseTypes = null,
        IReadOnlyList<DeclarationNode>? members = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, accessModifier, baseTypes, members, documentation, attributes, metadata)
    {
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        // Interface используется и в C#, и в TypeScript
        // Используем метод из IAstVisitor напрямую
        return visitor.VisitInterfaceDeclaration(this);
    }
    
    public override AstNode Clone()
    {
        return new InterfaceDeclarationNode(
            (IdentifierNode)Name.Clone(),
            AccessModifier,
            BaseTypes.Select(t => (TypeNode)t.Clone()).ToImmutableList(),
            Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
    
    public override string ToString()
    {
        var baseTypesStr = BaseTypes.Count > 0 
            ? $" : {string.Join(", ", BaseTypes)}" 
            : "";
        var membersCount = Members.Count;
        return $"interface {Name.Name}{baseTypesStr} [{membersCount} members]";
    }
}
