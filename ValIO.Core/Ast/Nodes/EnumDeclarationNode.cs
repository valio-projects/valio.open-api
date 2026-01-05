using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел объявления enum (общий для C# и TypeScript)
/// </summary>
public sealed class EnumDeclarationNode : TypeDeclarationNode
{
    /// <summary>
    /// Базовый тип enum (например, int, byte) - используется в C#
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
        // Enum используется и в C#, и в TypeScript
        // Проверяем, какой visitor используется
        if (visitor is ICSharpAstVisitor<TResult> csharpVisitor)
        {
            return csharpVisitor.VisitEnumDeclaration(this);
        }
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitEnumDeclaration(this);
        }
        throw new NotSupportedException($"Visitor must implement ICSharpAstVisitor<TResult> or ITypeScriptAstVisitor<TResult> to visit EnumDeclarationNode");
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
