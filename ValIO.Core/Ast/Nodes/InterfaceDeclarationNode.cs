using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

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
        // Проверяем, какой visitor используется
        if (visitor is ICSharpAstVisitor<TResult> csharpVisitor)
        {
            return csharpVisitor.VisitInterfaceDeclaration(this);
        }
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitInterfaceDeclaration(this);
        }
        throw new NotSupportedException($"Visitor must implement ICSharpAstVisitor<TResult> or ITypeScriptAstVisitor<TResult> to visit InterfaceDeclarationNode");
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
