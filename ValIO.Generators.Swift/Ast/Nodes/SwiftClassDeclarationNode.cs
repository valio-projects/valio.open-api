using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>
/// Swift <c>class</c> declaration. Used for HTTP-client services (e.g.
/// <c>public final class UserApi { ... }</c>).
/// </summary>
public sealed class SwiftClassDeclarationNode : DeclarationNode
{
    /// <summary>Stored properties.</summary>
    public IReadOnlyList<SwiftPropertyNode> Properties { get; }

    /// <summary>Member declarations (initializers + functions).</summary>
    public IReadOnlyList<DeclarationNode> Members { get; }

    /// <summary>Conformed protocols / inherited base class.</summary>
    public IReadOnlyList<string> Conformances { get; }

    /// <summary>Access modifier (e.g. <c>public</c>). Null = default internal.</summary>
    public string? AccessModifier { get; }

    /// <summary>Whether the class is marked <c>final</c>.</summary>
    public bool IsFinal { get; }

    /// <summary>Swift attributes (e.g. <c>@MainActor</c>).</summary>
    public IReadOnlyList<SwiftAttributeNode> SwiftAttributes { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(SwiftAttributes);
            children.AddRange(Properties);
            children.AddRange(Members);
            return children;
        }
    }

    public SwiftClassDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<SwiftPropertyNode>? properties = null,
        IReadOnlyList<DeclarationNode>? members = null,
        IReadOnlyList<string>? conformances = null,
        string? accessModifier = null,
        bool isFinal = false,
        IReadOnlyList<SwiftAttributeNode>? attributes = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Properties = properties?.ToImmutableList() ?? ImmutableList<SwiftPropertyNode>.Empty;
        Members = members?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
        Conformances = conformances?.ToImmutableList() ?? ImmutableList<string>.Empty;
        AccessModifier = accessModifier;
        IsFinal = isFinal;
        SwiftAttributes = attributes?.ToImmutableList() ?? ImmutableList<SwiftAttributeNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ISwiftAstVisitor<TResult> swiftVisitor)
            return swiftVisitor.VisitSwiftClassDeclaration(this);
        throw new NotSupportedException("Visitor must implement ISwiftAstVisitor<TResult> to visit SwiftClassDeclarationNode");
    }

    public override AstNode Clone() =>
        new SwiftClassDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Properties.Select(p => (SwiftPropertyNode)p.Clone()).ToImmutableList(),
            Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
            Conformances.ToImmutableList(),
            AccessModifier,
            IsFinal,
            SwiftAttributes.Select(a => (SwiftAttributeNode)a.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
