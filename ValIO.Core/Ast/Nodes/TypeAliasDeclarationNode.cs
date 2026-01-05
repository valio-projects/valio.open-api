using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел объявления type alias (TypeScript)
/// </summary>
public sealed class TypeAliasDeclarationNode : DeclarationNode
{
    /// <summary>
    /// Тип алиаса
    /// </summary>
    public TypeNode Type { get; }
    
    public override AstNodeKind Kind => AstNodeKind.TypeAliasDeclaration;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, Type };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public TypeAliasDeclarationNode(
        IdentifierNode name,
        TypeNode type,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes, metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitTypeAliasDeclaration(this);
        }
        throw new NotSupportedException($"Visitor must implement ITypeScriptAstVisitor<TResult> to visit TypeAliasDeclarationNode");
    }
    
    public override AstNode Clone()
    {
        return new TypeAliasDeclarationNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
