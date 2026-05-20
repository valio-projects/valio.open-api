using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>
/// Rust struct declaration:
///   <c>#[derive(Debug, Clone, Serialize, Deserialize)]\npub struct Name { fields... }</c>
/// </summary>
public sealed class RustStructNode : DeclarationNode
{
    /// <summary>Struct fields.</summary>
    public IReadOnlyList<RustFieldNode> Fields { get; }

    /// <summary>Derive macros (e.g. <c>Debug</c>, <c>Clone</c>, <c>Serialize</c>, <c>Deserialize</c>).</summary>
    public IReadOnlyList<string> Derives { get; }

    /// <summary>Visibility prefix (default <c>pub</c>).</summary>
    public string Visibility { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(Fields);
            return children;
        }
    }

    public RustStructNode(
        IdentifierNode name,
        IReadOnlyList<RustFieldNode>? fields = null,
        IReadOnlyList<string>? derives = null,
        string visibility = "pub",
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Fields = fields?.ToImmutableList() ?? ImmutableList<RustFieldNode>.Empty;
        Derives = derives?.ToImmutableList()
            ?? ImmutableList.Create("Debug", "Clone", "Serialize", "Deserialize");
        Visibility = visibility;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IRustAstVisitor<TResult> rustVisitor)
            return rustVisitor.VisitRustStruct(this);
        throw new NotSupportedException("Visitor must implement IRustAstVisitor<TResult> to visit RustStructNode");
    }

    public override AstNode Clone() =>
        new RustStructNode(
            (IdentifierNode)Name.Clone(),
            Fields.Select(f => (RustFieldNode)f.Clone()).ToImmutableList(),
            Derives.ToImmutableList(),
            Visibility,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
