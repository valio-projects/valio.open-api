using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>
/// Kotlin <c>enum class</c> declaration with annotated members.
/// </summary>
public sealed class KotlinEnumDeclarationNode : DeclarationNode
{
    public IReadOnlyList<KotlinAnnotationNode> KotlinAnnotations { get; }
    public IReadOnlyList<KotlinEnumMemberNode> Members { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(KotlinAnnotations);
            children.AddRange(Members);
            return children;
        }
    }

    public KotlinEnumDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<KotlinEnumMemberNode>? members = null,
        IReadOnlyList<KotlinAnnotationNode>? annotations = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Members = members?.ToImmutableList() ?? ImmutableList<KotlinEnumMemberNode>.Empty;
        KotlinAnnotations = annotations?.ToImmutableList() ?? ImmutableList<KotlinAnnotationNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IKotlinAstVisitor<TResult> ktVisitor)
            return ktVisitor.VisitKotlinEnumDeclaration(this);
        throw new NotSupportedException("Visitor must implement IKotlinAstVisitor<TResult> to visit KotlinEnumDeclarationNode");
    }

    public override AstNode Clone() =>
        new KotlinEnumDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Members.Select(m => (KotlinEnumMemberNode)m.Clone()).ToImmutableList(),
            KotlinAnnotations.Select(a => (KotlinAnnotationNode)a.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
