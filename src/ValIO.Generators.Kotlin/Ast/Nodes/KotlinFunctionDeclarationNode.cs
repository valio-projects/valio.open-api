using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>
/// Kotlin function declaration:
///   <c>[suspend] fun name(p: T): R { body }</c>
///   <c>[suspend] fun name(p: T): R = expression</c>
/// Both statement blocks and expression bodies carry structured syntax.
/// </summary>
public sealed class KotlinFunctionDeclarationNode : DeclarationNode
{
    /// <summary>If true, emit the <c>suspend</c> modifier.</summary>
    public bool IsSuspend { get; }

    /// <summary>Parameters.</summary>
    public IReadOnlyList<KotlinParameterNode> Parameters { get; }

    /// <summary>Return type. Null = <c>Unit</c> (omit annotation).</summary>
    public TypeNode? ReturnType { get; }

    /// <summary>Gets a structured function body when the declaration uses shared statement AST nodes.</summary>
    /// <value>The body block when the function is declared with braces.</value>
    public BlockNode? StructuredBody { get; }

    /// <summary>Gets whether this declaration implements an inherited or interface function.</summary>
    /// <value>True to emit the Kotlin override modifier.</value>
    public bool IsOverride { get; }

    /// <summary>Function-level annotations.</summary>
    public IReadOnlyList<KotlinAnnotationNode> KotlinAnnotations { get; }

    /// <summary>Visibility modifier (e.g. <c>private</c>, <c>internal</c>).</summary>
    public string? Visibility { get; }

    /// <summary>Gets the result expression of an expression-bodied declaration.</summary>
    /// <value>The expression printed after the equals sign, if present.</value>
    public ExpressionNode? ExpressionBody { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(KotlinAnnotations);
            children.AddRange(Parameters);
            if (ReturnType != null) children.Add(ReturnType);
            if (StructuredBody != null) children.Add(StructuredBody);
            if (ExpressionBody is not null) children.Add(ExpressionBody);
            return children;
        }
    }

    /// <summary>Creates a function declaration with one explicit body representation.</summary>
    /// <param name="name">The function identifier.</param>
    /// <param name="parameters">The ordered function parameters.</param>
    /// <param name="returnType">The return type, or null to omit Unit.</param>
    /// <param name="isSuspend">Whether the function can suspend.</param>
    /// <param name="annotations">The function annotations.</param>
    /// <param name="visibility">The optional Kotlin visibility modifier.</param>
    /// <param name="documentation">The optional generated documentation.</param>
    /// <param name="expressionBody">An expression-bodied result.</param>
    /// <param name="metadata">Optional node metadata.</param>
    /// <param name="structuredBody">The structured statement block.</param>
    /// <param name="isOverride">Whether the function implements an inherited declaration.</param>
    /// <exception cref="ArgumentException">Both expression and block bodies are specified.</exception>
    public KotlinFunctionDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<KotlinParameterNode>? parameters = null,
        TypeNode? returnType = null,
        bool isSuspend = false,
        IReadOnlyList<KotlinAnnotationNode>? annotations = null,
        string? visibility = null,
        CommentNode? documentation = null,
        ExpressionNode? expressionBody = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        BlockNode? structuredBody = null,
        bool isOverride = false)
        : base(name, documentation, attributes: null, metadata)
    {
        IsSuspend = isSuspend;
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<KotlinParameterNode>.Empty;
        ReturnType = returnType;
        KotlinAnnotations = annotations?.ToImmutableList() ?? ImmutableList<KotlinAnnotationNode>.Empty;
        Visibility = visibility;
        ExpressionBody = expressionBody;
        StructuredBody = structuredBody;
        IsOverride = isOverride;
        if (structuredBody is not null && expressionBody is not null)
            throw new ArgumentException("Choose one function body.", nameof(structuredBody));
    }

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IKotlinAstVisitor<TResult> ktVisitor)
            return ktVisitor.VisitKotlinFunctionDeclaration(this);
        throw new NotSupportedException("Visitor must implement IKotlinAstVisitor<TResult> to visit KotlinFunctionDeclarationNode");
    }

    /// <inheritdoc/>
    public override AstNode Clone() =>
        new KotlinFunctionDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Parameters.Select(p => (KotlinParameterNode)p.Clone()).ToImmutableList(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            IsSuspend,
            KotlinAnnotations.Select(a => (KotlinAnnotationNode)a.Clone()).ToImmutableList(),
            Visibility,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            (ExpressionNode?)ExpressionBody?.Clone(),
            Metadata.ToImmutableDictionary(),
            StructuredBody == null ? null : (BlockNode)StructuredBody.Clone(),
            IsOverride);
}
