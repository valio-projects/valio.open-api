using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>
/// Kotlin annotation reference: <c>@Serializable</c>, <c>@SerialName("email")</c>.
/// Arguments are pre-rendered Kotlin expressions to keep the AST minimal.
/// </summary>
public sealed class KotlinAnnotationNode : AstNode
{
    /// <summary>Annotation name (e.g. <c>Serializable</c>, <c>SerialName</c>).</summary>
    public string Name { get; }

    /// <summary>Argument literals (pre-rendered, e.g. <c>"email"</c>).</summary>
    public IReadOnlyList<string> Arguments { get; }

    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();

    public KotlinAnnotationNode(
        string name,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Annotation name cannot be null or whitespace", nameof(name));
        Name = name;
        Arguments = arguments?.ToImmutableList() ?? ImmutableList<string>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IKotlinAstVisitor<TResult> ktVisitor)
            return ktVisitor.VisitKotlinAnnotation(this);
        throw new NotSupportedException("Visitor must implement IKotlinAstVisitor<TResult> to visit KotlinAnnotationNode");
    }

    public override AstNode Clone() =>
        new KotlinAnnotationNode(Name, Arguments.ToImmutableList(), Metadata.ToImmutableDictionary());
}
