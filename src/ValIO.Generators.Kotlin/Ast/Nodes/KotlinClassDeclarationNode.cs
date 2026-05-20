using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>
/// Regular Kotlin <c>class</c> with a primary constructor and member functions.
/// Used for HTTP-client services (e.g. <c>PetApi(client, baseUrl, json)</c>).
/// </summary>
public sealed class KotlinClassDeclarationNode : DeclarationNode
{
    /// <summary>Class-level annotations.</summary>
    public IReadOnlyList<KotlinAnnotationNode> KotlinAnnotations { get; }

    /// <summary>Primary constructor properties (each becomes <c>val name: T</c> in parens).</summary>
    public IReadOnlyList<KotlinPropertyNode> ConstructorProperties { get; }

    /// <summary>Body members (functions).</summary>
    public IReadOnlyList<DeclarationNode> Members { get; }

    /// <summary>Visibility modifier (e.g. <c>internal</c>). Null = default public.</summary>
    public string? Visibility { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(KotlinAnnotations);
            children.AddRange(ConstructorProperties);
            children.AddRange(Members);
            return children;
        }
    }

    public KotlinClassDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<KotlinPropertyNode>? constructorProperties = null,
        IReadOnlyList<DeclarationNode>? members = null,
        IReadOnlyList<KotlinAnnotationNode>? annotations = null,
        string? visibility = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        ConstructorProperties = constructorProperties?.ToImmutableList() ?? ImmutableList<KotlinPropertyNode>.Empty;
        Members = members?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
        KotlinAnnotations = annotations?.ToImmutableList() ?? ImmutableList<KotlinAnnotationNode>.Empty;
        Visibility = visibility;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IKotlinAstVisitor<TResult> ktVisitor)
            return ktVisitor.VisitKotlinClassDeclaration(this);
        throw new NotSupportedException("Visitor must implement IKotlinAstVisitor<TResult> to visit KotlinClassDeclarationNode");
    }

    public override AstNode Clone() =>
        new KotlinClassDeclarationNode(
            (IdentifierNode)Name.Clone(),
            ConstructorProperties.Select(p => (KotlinPropertyNode)p.Clone()).ToImmutableList(),
            Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
            KotlinAnnotations.Select(a => (KotlinAnnotationNode)a.Clone()).ToImmutableList(),
            Visibility,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
