using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>
/// Swift <c>enum</c> declaration:
///   <c>[access] enum Name: RawType, Codable, Sendable { case ... }</c>
/// </summary>
public sealed class SwiftEnumDeclarationNode : DeclarationNode
{
    public IReadOnlyList<SwiftEnumMemberNode> Members { get; }
    public IReadOnlyList<string> Conformances { get; }
    public string? RawType { get; }
    public string? AccessModifier { get; }
    public IReadOnlyList<SwiftAttributeNode> SwiftAttributes { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(SwiftAttributes);
            children.AddRange(Members);
            return children;
        }
    }

    public SwiftEnumDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<SwiftEnumMemberNode>? members = null,
        IReadOnlyList<string>? conformances = null,
        string? rawType = null,
        string? accessModifier = null,
        IReadOnlyList<SwiftAttributeNode>? attributes = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Members = members?.ToImmutableList() ?? ImmutableList<SwiftEnumMemberNode>.Empty;
        Conformances = conformances?.ToImmutableList() ?? ImmutableList<string>.Empty;
        RawType = rawType;
        AccessModifier = accessModifier;
        SwiftAttributes = attributes?.ToImmutableList() ?? ImmutableList<SwiftAttributeNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ISwiftAstVisitor<TResult> swiftVisitor)
            return swiftVisitor.VisitSwiftEnumDeclaration(this);
        throw new NotSupportedException("Visitor must implement ISwiftAstVisitor<TResult> to visit SwiftEnumDeclarationNode");
    }

    public override AstNode Clone() =>
        new SwiftEnumDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Members.Select(m => (SwiftEnumMemberNode)m.Clone()).ToImmutableList(),
            Conformances.ToImmutableList(),
            RawType,
            AccessModifier,
            SwiftAttributes.Select(a => (SwiftAttributeNode)a.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
