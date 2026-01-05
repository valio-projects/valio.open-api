using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Visitors;

namespace ValIO.Generators.CSharp.Ast.Nodes;

/// <summary>
/// Узел объявления интерфейса (C#)
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
        if (visitor is ICSharpAstVisitor<TResult> csharpVisitor)
        {
            return csharpVisitor.VisitInterfaceDeclaration(this);
        }
        throw new NotSupportedException($"Visitor must implement ICSharpAstVisitor<TResult> to visit InterfaceDeclarationNode");
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
}
