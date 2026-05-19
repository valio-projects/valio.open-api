using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>
/// Kotlin <c>data class</c> with a primary constructor:
///   <c>@Annotations</c> <c>data class Name(val foo: Foo, val bar: Bar = null)</c>
/// </summary>
public sealed class KotlinDataClassNode : DeclarationNode
{
    /// <summary>Class-level annotations (e.g. <c>@Serializable</c>).</summary>
    public IReadOnlyList<KotlinAnnotationNode> KotlinAnnotations { get; }

    /// <summary>Primary constructor properties (rendered inside the parens).</summary>
    public IReadOnlyList<KotlinPropertyNode> Properties { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(KotlinAnnotations);
            children.AddRange(Properties);
            return children;
        }
    }

    public KotlinDataClassNode(
        IdentifierNode name,
        IReadOnlyList<KotlinPropertyNode>? properties = null,
        IReadOnlyList<KotlinAnnotationNode>? annotations = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Properties = properties?.ToImmutableList() ?? ImmutableList<KotlinPropertyNode>.Empty;
        KotlinAnnotations = annotations?.ToImmutableList() ?? ImmutableList<KotlinAnnotationNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IKotlinAstVisitor<TResult> ktVisitor)
            return ktVisitor.VisitKotlinDataClass(this);
        throw new NotSupportedException("Visitor must implement IKotlinAstVisitor<TResult> to visit KotlinDataClassNode");
    }

    public override AstNode Clone() =>
        new KotlinDataClassNode(
            (IdentifierNode)Name.Clone(),
            Properties.Select(p => (KotlinPropertyNode)p.Clone()).ToImmutableList(),
            KotlinAnnotations.Select(a => (KotlinAnnotationNode)a.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
