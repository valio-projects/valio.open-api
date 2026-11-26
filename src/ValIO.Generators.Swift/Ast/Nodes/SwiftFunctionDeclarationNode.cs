using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>
/// Swift function declaration:
///   <c>[attrs] [access] func name(params) [async] [throws] -> Return { body }</c>
/// The body is a block of Swift statement nodes.
/// </summary>
public sealed class SwiftFunctionDeclarationNode : DeclarationNode
{
    /// <summary>Parameters.</summary>
    public IReadOnlyList<SwiftParameterNode> Parameters { get; }

    /// <summary>Return type. Null = <c>Void</c> (omitted in source).</summary>
    public TypeNode? ReturnType { get; }

    /// <summary>If true, emit the <c>async</c> modifier.</summary>
    public bool IsAsync { get; }

    /// <summary>If true, emit the <c>throws</c> modifier.</summary>
    public bool IsThrowing { get; }

    /// <summary>Gets the executable statements in the function.</summary>
    /// <value>The complete body AST.</value>
    public BlockNode Body { get; }

    /// <summary>Access modifier (e.g. <c>public</c>, <c>private</c>).</summary>
    public string? AccessModifier { get; }

    /// <summary>If true, emit the <c>static</c> modifier.</summary>
    public bool IsStatic { get; }

    /// <summary>Swift attributes attached to the function (e.g. <c>@discardableResult</c>).</summary>
    public IReadOnlyList<SwiftAttributeNode> SwiftAttributes { get; }

    /// <summary>True for initializers (rendered as <c>init(...)</c> rather than <c>func name(...)</c>).</summary>
    public bool IsInitializer { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(SwiftAttributes);
            children.AddRange(Parameters);
            if (ReturnType != null) children.Add(ReturnType);
            children.Add(Body);
            return children;
        }
    }

    /// <summary>Creates a Swift function or initializer declaration.</summary>
    /// <param name="name">The declaration identifier.</param>
    /// <param name="parameters">The arguments in signature order.</param>
    /// <param name="returnType">The optional result annotation.</param>
    /// <param name="body">The ordered statements to execute.</param>
    /// <param name="isAsync">Whether the declaration can await.</param>
    /// <param name="isThrowing">Whether it can propagate an error.</param>
    /// <param name="accessModifier">The optional Swift access modifier.</param>
    /// <param name="isStatic">Whether it belongs to the type itself.</param>
    /// <param name="attributes">The attached Swift attributes.</param>
    /// <param name="documentation">The optional documentation comment.</param>
    /// <param name="isInitializer">Whether to render the member as an initializer.</param>
    /// <param name="metadata">The associated AST metadata.</param>
    public SwiftFunctionDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<SwiftParameterNode>? parameters = null,
        TypeNode? returnType = null,
        BlockNode? body = null,
        bool isAsync = false,
        bool isThrowing = false,
        string? accessModifier = null,
        bool isStatic = false,
        IReadOnlyList<SwiftAttributeNode>? attributes = null,
        CommentNode? documentation = null,
        bool isInitializer = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<SwiftParameterNode>.Empty;
        ReturnType = returnType;
        Body = body ?? new BlockNode();
        IsAsync = isAsync;
        IsThrowing = isThrowing;
        AccessModifier = accessModifier;
        IsStatic = isStatic;
        SwiftAttributes = attributes?.ToImmutableList() ?? ImmutableList<SwiftAttributeNode>.Empty;
        IsInitializer = isInitializer;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ISwiftAstVisitor<TResult> swiftVisitor)
            return swiftVisitor.VisitSwiftFunctionDeclaration(this);
        throw new NotSupportedException("Visitor must implement ISwiftAstVisitor<TResult> to visit SwiftFunctionDeclarationNode");
    }

    /// <inheritdoc/>
    public override AstNode Clone() =>
        new SwiftFunctionDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Parameters.Select(p => (SwiftParameterNode)p.Clone()).ToImmutableList(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            (BlockNode)Body.Clone(),
            IsAsync,
            IsThrowing,
            AccessModifier,
            IsStatic,
            SwiftAttributes.Select(a => (SwiftAttributeNode)a.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            IsInitializer,
            Metadata.ToImmutableDictionary());
}
