using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>
/// Rust enum declaration:
///   <c>#[derive(Debug, Clone, Serialize, Deserialize)]\n#[serde(rename_all = "camelCase")]\npub enum Name { variants... }</c>
/// </summary>
public sealed class RustEnumNode : DeclarationNode
{
    /// <summary>Enum variants.</summary>
    public IReadOnlyList<RustEnumVariantNode> Variants { get; }

    /// <summary>Derive macros (default <c>Debug, Clone, Serialize, Deserialize</c>).</summary>
    public IReadOnlyList<string> Derives { get; }

    /// <summary>
    /// Argument for <c>#[serde(rename_all = "...")]</c> (default <c>camelCase</c>).
    /// Pass null to omit the attribute.
    /// </summary>
    public string? SerdeRenameAll { get; }

    /// <summary>Visibility prefix (default <c>pub</c>).</summary>
    public string Visibility { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(Variants);
            return children;
        }
    }

    public RustEnumNode(
        IdentifierNode name,
        IReadOnlyList<RustEnumVariantNode>? variants = null,
        IReadOnlyList<string>? derives = null,
        string? serdeRenameAll = "camelCase",
        string visibility = "pub",
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Variants = variants?.ToImmutableList() ?? ImmutableList<RustEnumVariantNode>.Empty;
        Derives = derives?.ToImmutableList()
            ?? ImmutableList.Create("Debug", "Clone", "Serialize", "Deserialize");
        SerdeRenameAll = serdeRenameAll;
        Visibility = visibility;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IRustAstVisitor<TResult> rustVisitor)
            return rustVisitor.VisitRustEnum(this);
        throw new NotSupportedException("Visitor must implement IRustAstVisitor<TResult> to visit RustEnumNode");
    }

    public override AstNode Clone() =>
        new RustEnumNode(
            (IdentifierNode)Name.Clone(),
            Variants.Select(v => (RustEnumVariantNode)v.Clone()).ToImmutableList(),
            Derives.ToImmutableList(),
            SerdeRenameAll,
            Visibility,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
