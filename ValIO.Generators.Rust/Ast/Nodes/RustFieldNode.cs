using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>
/// Rust struct field: <c>pub name: Type</c>, optionally preceded by
/// <c>#[serde(rename = "wire_name")]</c>.
/// </summary>
public sealed class RustFieldNode : DeclarationNode
{
    /// <summary>Field type annotation.</summary>
    public TypeNode Type { get; }

    /// <summary>Optional serde rename target (original schema property name).</summary>
    public string? SerdeRename { get; }

    /// <summary>Visibility (default <c>pub</c>).</summary>
    public string Visibility { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, Type };
            if (Documentation != null) children.Add(Documentation);
            return children;
        }
    }

    public RustFieldNode(
        IdentifierNode name,
        TypeNode type,
        string? serdeRename = null,
        string visibility = "pub",
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        SerdeRename = serdeRename;
        Visibility = visibility;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IRustAstVisitor<TResult> rustVisitor)
            return rustVisitor.VisitRustField(this);
        throw new NotSupportedException("Visitor must implement IRustAstVisitor<TResult> to visit RustFieldNode");
    }

    public override AstNode Clone() =>
        new RustFieldNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            SerdeRename,
            Visibility,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
