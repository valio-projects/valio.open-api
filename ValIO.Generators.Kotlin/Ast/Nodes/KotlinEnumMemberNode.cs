using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>
/// Single Kotlin enum entry, optionally preceded by annotations like
/// <c>@SerialName("admin")</c>.
/// </summary>
public sealed class KotlinEnumMemberNode : AstNode
{
    public string Name { get; }

    public IReadOnlyList<KotlinAnnotationNode> KotlinAnnotations { get; }

    public CommentNode? Documentation { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            children.AddRange(KotlinAnnotations);
            if (Documentation != null) children.Add(Documentation);
            return children;
        }
    }

    public KotlinEnumMemberNode(
        string name,
        IReadOnlyList<KotlinAnnotationNode>? annotations = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Enum member name required", nameof(name));
        Name = name;
        KotlinAnnotations = annotations?.ToImmutableList() ?? ImmutableList<KotlinAnnotationNode>.Empty;
        Documentation = documentation;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IKotlinAstVisitor<TResult> ktVisitor)
            return ktVisitor.VisitKotlinEnumMember(this);
        throw new NotSupportedException("Visitor must implement IKotlinAstVisitor<TResult> to visit KotlinEnumMemberNode");
    }

    public override AstNode Clone() =>
        new KotlinEnumMemberNode(
            Name,
            KotlinAnnotations.Select(a => (KotlinAnnotationNode)a.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
