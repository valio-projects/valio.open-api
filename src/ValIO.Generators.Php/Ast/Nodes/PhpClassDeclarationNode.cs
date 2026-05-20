using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Php.Ast.Visitors;

namespace ValIO.Generators.Php.Ast.Nodes;

/// <summary>
/// PHP class declaration (<c>final class</c> by default).
/// </summary>
public sealed class PhpClassDeclarationNode : TypeDeclarationNode
{
    public bool IsFinal { get; }
    public bool IsAbstract { get; }
    public bool IsReadonly { get; }
    public PhpConstructorDeclarationNode? Constructor { get; }
    public IReadOnlyList<string> Implements { get; }

    public PhpClassDeclarationNode(
        IdentifierNode name,
        bool isFinal = true,
        bool isAbstract = false,
        bool isReadonly = false,
        IReadOnlyList<string>? implements = null,
        PhpConstructorDeclarationNode? constructor = null,
        IReadOnlyList<DeclarationNode>? members = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, AccessModifier.Public, baseTypes: null, members: members, documentation: documentation, attributes: attributes, metadata: metadata)
    {
        IsFinal = isFinal;
        IsAbstract = isAbstract;
        IsReadonly = isReadonly;
        Constructor = constructor;
        Implements = implements?.ToImmutableList() ?? ImmutableList<string>.Empty;
    }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var c = new List<AstNode> { Name };
            if (Documentation != null) c.Add(Documentation);
            c.AddRange(Attributes);
            if (Constructor != null) c.Add(Constructor);
            c.AddRange(Members);
            return c;
        }
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPhpAstVisitor<TResult> phpVisitor)
            return phpVisitor.VisitPhpClassDeclaration(this);
        throw new NotSupportedException($"Visitor must implement IPhpAstVisitor<TResult> to visit {nameof(PhpClassDeclarationNode)}.");
    }

    public override AstNode Clone() => new PhpClassDeclarationNode(
        (IdentifierNode)Name.Clone(),
        IsFinal,
        IsAbstract,
        IsReadonly,
        Implements.ToImmutableList(),
        Constructor != null ? (PhpConstructorDeclarationNode)Constructor.Clone() : null,
        Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
        Documentation != null ? (CommentNode)Documentation.Clone() : null,
        Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
        Metadata.ToImmutableDictionary());
}
