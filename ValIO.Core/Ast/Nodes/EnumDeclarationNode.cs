using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел объявления enum (C#)
/// </summary>
public sealed class EnumDeclarationNode : TypeDeclarationNode
{
    /// <summary>
    /// Базовый тип enum (например, int, byte)
    /// </summary>
    public TypeNode? UnderlyingType { get; }
    
    public override AstNodeKind Kind => AstNodeKind.EnumDeclaration;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            if (UnderlyingType != null)
                children.Add(UnderlyingType);
            children.AddRange(Members);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public EnumDeclarationNode(
        IdentifierNode name,
        AccessModifier accessModifier = AccessModifier.Public,
        TypeNode? underlyingType = null,
        IReadOnlyList<DeclarationNode>? members = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, accessModifier, baseTypes: null, members, documentation, attributes, metadata)
    {
        UnderlyingType = underlyingType;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitEnumDeclaration(this);
    }
    
    public override AstNode Clone()
    {
        return new EnumDeclarationNode(
            (IdentifierNode)Name.Clone(),
            AccessModifier,
            UnderlyingType != null ? (TypeNode)UnderlyingType.Clone() : null,
            Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
