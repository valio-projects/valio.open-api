using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>
/// Swift struct declaration:
///   <c>[access] struct Name: Protocol1, Protocol2 { properties... }</c>
/// </summary>
public sealed class SwiftStructDeclarationNode : DeclarationNode
{
    /// <summary>Stored properties (<c>let</c>/<c>var</c>).</summary>
    public IReadOnlyList<SwiftPropertyNode> Properties { get; }

    /// <summary>Protocols the struct conforms to (e.g. <c>Codable</c>, <c>Sendable</c>).</summary>
    public IReadOnlyList<string> Conformances { get; }

    /// <summary>Access modifier (e.g. <c>public</c>). Null = default internal.</summary>
    public string? AccessModifier { get; }

    /// <summary>Swift-level attributes.</summary>
    public IReadOnlyList<SwiftAttributeNode> SwiftAttributes { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(SwiftAttributes);
            children.AddRange(Properties);
            return children;
        }
    }

    public SwiftStructDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<SwiftPropertyNode>? properties = null,
        IReadOnlyList<string>? conformances = null,
        string? accessModifier = null,
        IReadOnlyList<SwiftAttributeNode>? attributes = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Properties = properties?.ToImmutableList() ?? ImmutableList<SwiftPropertyNode>.Empty;
        Conformances = conformances?.ToImmutableList() ?? ImmutableList<string>.Empty;
        AccessModifier = accessModifier;
        SwiftAttributes = attributes?.ToImmutableList() ?? ImmutableList<SwiftAttributeNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ISwiftAstVisitor<TResult> swiftVisitor)
            return swiftVisitor.VisitSwiftStruct(this);
        throw new NotSupportedException("Visitor must implement ISwiftAstVisitor<TResult> to visit SwiftStructDeclarationNode");
    }

    public override AstNode Clone() =>
        new SwiftStructDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Properties.Select(p => (SwiftPropertyNode)p.Clone()).ToImmutableList(),
            Conformances.ToImmutableList(),
            AccessModifier,
            SwiftAttributes.Select(a => (SwiftAttributeNode)a.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
