using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел объявления struct (C#)
/// </summary>
public sealed class StructDeclarationNode : TypeDeclarationNode
{
    /// <summary>
    /// Является ли struct readonly
    /// </summary>
    public bool IsReadOnly { get; }
    
    public override AstNodeKind Kind => AstNodeKind.StructDeclaration;
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public StructDeclarationNode(
        IdentifierNode name,
        AccessModifier accessModifier = AccessModifier.Public,
        bool isReadOnly = false,
        IReadOnlyList<TypeNode>? baseTypes = null,
        IReadOnlyList<DeclarationNode>? members = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, accessModifier, baseTypes, members, documentation, attributes, metadata)
    {
        IsReadOnly = isReadOnly;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICSharpAstVisitor<TResult> csharpVisitor)
        {
            return csharpVisitor.VisitStructDeclaration(this);
        }
        throw new NotSupportedException($"Visitor must implement ICSharpAstVisitor<TResult> to visit StructDeclarationNode");
    }
    
    public override AstNode Clone()
    {
        return new StructDeclarationNode(
            (IdentifierNode)Name.Clone(),
            AccessModifier,
            IsReadOnly,
            BaseTypes.Select(t => (TypeNode)t.Clone()).ToImmutableList(),
            Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
