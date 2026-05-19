using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>
/// Single Swift enum case: <c>case admin = "admin"</c>.
/// </summary>
public sealed class SwiftEnumMemberNode : AstNode
{
    /// <summary>Identifier name of the case (lowerCamelCase).</summary>
    public string Name { get; }

    /// <summary>Optional raw-value literal (pre-rendered) — e.g. <c>"admin"</c>.</summary>
    public string? RawValueLiteral { get; }

    public CommentNode? Documentation { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (Documentation != null) children.Add(Documentation);
            return children;
        }
    }

    public SwiftEnumMemberNode(
        string name,
        string? rawValueLiteral = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Enum member name required", nameof(name));
        Name = name;
        RawValueLiteral = rawValueLiteral;
        Documentation = documentation;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ISwiftAstVisitor<TResult> swiftVisitor)
            return swiftVisitor.VisitSwiftEnumMember(this);
        throw new NotSupportedException("Visitor must implement ISwiftAstVisitor<TResult> to visit SwiftEnumMemberNode");
    }

    public override AstNode Clone() =>
        new SwiftEnumMemberNode(
            Name,
            RawValueLiteral,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
