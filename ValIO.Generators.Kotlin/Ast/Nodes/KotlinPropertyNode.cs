using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>
/// Kotlin property declaration used as a data-class primary-constructor parameter:
///   <c>[annotations] val name: Type = default</c>
/// or as a regular class property (when <see cref="IsConstructorParameter"/> is false).
/// </summary>
public sealed class KotlinPropertyNode : DeclarationNode
{
    /// <summary>Property type annotation.</summary>
    public TypeNode Type { get; }

    /// <summary>Default value literal (pre-rendered) or null when none.</summary>
    public string? DefaultValueLiteral { get; }

    /// <summary>If true, declared with <c>val</c>; otherwise <c>var</c>.</summary>
    public bool IsReadOnly { get; }

    /// <summary>True when this property is emitted inside a primary constructor.</summary>
    public bool IsConstructorParameter { get; }

    /// <summary>Visibility modifier (e.g. <c>private</c>). Null = default public.</summary>
    public string? Visibility { get; }

    /// <summary>Property-level annotations.</summary>
    public IReadOnlyList<KotlinAnnotationNode> KotlinAnnotations { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, Type };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(KotlinAnnotations);
            return children;
        }
    }

    public KotlinPropertyNode(
        IdentifierNode name,
        TypeNode type,
        string? defaultValueLiteral = null,
        bool isReadOnly = true,
        bool isConstructorParameter = true,
        string? visibility = null,
        IReadOnlyList<KotlinAnnotationNode>? annotations = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        DefaultValueLiteral = defaultValueLiteral;
        IsReadOnly = isReadOnly;
        IsConstructorParameter = isConstructorParameter;
        Visibility = visibility;
        KotlinAnnotations = annotations?.ToImmutableList() ?? ImmutableList<KotlinAnnotationNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IKotlinAstVisitor<TResult> ktVisitor)
            return ktVisitor.VisitKotlinProperty(this);
        throw new NotSupportedException("Visitor must implement IKotlinAstVisitor<TResult> to visit KotlinPropertyNode");
    }

    public override AstNode Clone() =>
        new KotlinPropertyNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            DefaultValueLiteral,
            IsReadOnly,
            IsConstructorParameter,
            Visibility,
            KotlinAnnotations.Select(a => (KotlinAnnotationNode)a.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
