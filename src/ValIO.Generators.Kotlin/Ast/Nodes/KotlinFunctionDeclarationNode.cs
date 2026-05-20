using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>
/// Kotlin function declaration:
///   <c>[suspend] fun name(p: T): R { body }</c>
///   <c>[suspend] fun name(p: T): R = expression</c>
/// Body lines are pre-rendered strings to keep the AST minimal — the renderer
/// indents them under the function brace.
/// </summary>
public sealed class KotlinFunctionDeclarationNode : DeclarationNode
{
    /// <summary>If true, emit the <c>suspend</c> modifier.</summary>
    public bool IsSuspend { get; }

    /// <summary>Parameters.</summary>
    public IReadOnlyList<KotlinParameterNode> Parameters { get; }

    /// <summary>Return type. Null = <c>Unit</c> (omit annotation).</summary>
    public TypeNode? ReturnType { get; }

    /// <summary>Body lines (pre-rendered).</summary>
    public IReadOnlyList<string> Body { get; }

    /// <summary>Function-level annotations.</summary>
    public IReadOnlyList<KotlinAnnotationNode> KotlinAnnotations { get; }

    /// <summary>Visibility modifier (e.g. <c>private</c>, <c>internal</c>).</summary>
    public string? Visibility { get; }

    /// <summary>
    /// When true, emit the function as <c>fun name(...) = expression</c>
    /// (the body's first non-blank line is used as the expression).
    /// </summary>
    public bool IsExpressionBody { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(KotlinAnnotations);
            children.AddRange(Parameters);
            if (ReturnType != null) children.Add(ReturnType);
            return children;
        }
    }

    public KotlinFunctionDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<KotlinParameterNode>? parameters = null,
        TypeNode? returnType = null,
        IReadOnlyList<string>? body = null,
        bool isSuspend = false,
        IReadOnlyList<KotlinAnnotationNode>? annotations = null,
        string? visibility = null,
        CommentNode? documentation = null,
        bool isExpressionBody = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        IsSuspend = isSuspend;
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<KotlinParameterNode>.Empty;
        ReturnType = returnType;
        Body = body?.ToImmutableList() ?? ImmutableList<string>.Empty;
        KotlinAnnotations = annotations?.ToImmutableList() ?? ImmutableList<KotlinAnnotationNode>.Empty;
        Visibility = visibility;
        IsExpressionBody = isExpressionBody;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IKotlinAstVisitor<TResult> ktVisitor)
            return ktVisitor.VisitKotlinFunctionDeclaration(this);
        throw new NotSupportedException("Visitor must implement IKotlinAstVisitor<TResult> to visit KotlinFunctionDeclarationNode");
    }

    public override AstNode Clone() =>
        new KotlinFunctionDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Parameters.Select(p => (KotlinParameterNode)p.Clone()).ToImmutableList(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            Body.ToImmutableList(),
            IsSuspend,
            KotlinAnnotations.Select(a => (KotlinAnnotationNode)a.Clone()).ToImmutableList(),
            Visibility,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            IsExpressionBody,
            Metadata.ToImmutableDictionary());
}
