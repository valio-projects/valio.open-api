using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>
/// Rust function parameter (<c>name: Type</c>).
/// </summary>
public sealed class RustParameter : IEquatable<RustParameter>
{
    /// <summary>Initializes the values carried by this model.</summary>
    /// <param name="Name">The parameter name emitted with the function.</param>
    /// <param name="Type">The declared Rust type.</param>
    public RustParameter(string Name, TypeNode Type)
    {
        this.Name = Name;
        this.Type = Type;
    }

    /// <summary>Gets the parameter name emitted with the function.</summary>
    /// <value>The parameter name emitted with the function.</value>
    public string Name { get; init; }

    /// <summary>Gets the declared Rust type.</summary>
    /// <value>The declared Rust type.</value>
    public TypeNode Type { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(RustParameter? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Name, other.Name)
        && EqualityComparer<TypeNode>.Default.Equals(Type, other.Type);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RustParameter other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, Type);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(RustParameter? left, RustParameter? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(RustParameter? left, RustParameter? right) => !Equals(left, right);
}

/// <summary>
/// Rust function declaration:
///   <c>pub async fn name(params) -&gt; Result&lt;R, reqwest::Error&gt; { body }</c>
/// Carries the ordered statements and the optional final value expression.
/// </summary>
public sealed class RustFunctionNode : DeclarationNode
{
    /// <summary>Parameters.</summary>
    public IReadOnlyList<RustParameter> Parameters { get; }

    /// <summary>Return type. Null = unit (omitted in source).</summary>
    public TypeNode? ReturnType { get; }

    /// <summary>Gets the statements executed by the function.</summary>
    /// <value>The function body AST.</value>
    public BlockNode Body { get; }

    /// <summary>Gets the final expression returned without a trailing semicolon.</summary>
    /// <value>The implicit result expression, if present.</value>
    public ExpressionNode? Tail { get; }

    /// <summary>True for <c>async fn</c>.</summary>
    public bool IsAsync { get; }

    /// <summary>Visibility (default <c>pub</c>).</summary>
    public string Visibility { get; }

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            foreach (var p in Parameters)
                children.Add(p.Type);
            if (ReturnType != null) children.Add(ReturnType);
            children.Add(Body);
            if (Tail is not null) children.Add(Tail);
            return children;
        }
    }

    /// <summary>Creates a Rust function declaration.</summary>
    /// <param name="name">The function name.</param>
    /// <param name="parameters">The positional parameters.</param>
    /// <param name="returnType">The optional result type.</param>
    /// <param name="body">The executable body.</param>
    /// <param name="isAsync">Whether the function can await.</param>
    /// <param name="visibility">The Rust visibility token.</param>
    /// <param name="documentation">The optional rustdoc comment.</param>
    /// <param name="metadata">The optional node metadata.</param>
    /// <param name="tail">The implicit result of the function.</param>
    public RustFunctionNode(
        IdentifierNode name,
        IReadOnlyList<RustParameter>? parameters = null,
        TypeNode? returnType = null,
        BlockNode? body = null,
        bool isAsync = true,
        string visibility = "pub",
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        ExpressionNode? tail = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<RustParameter>.Empty;
        ReturnType = returnType;
        Body = body ?? new BlockNode();
        Tail = tail;
        IsAsync = isAsync;
        Visibility = visibility;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IRustAstVisitor<TResult> rustVisitor)
            return rustVisitor.VisitRustFunction(this);
        throw new NotSupportedException("Visitor must implement IRustAstVisitor<TResult> to visit RustFunctionNode");
    }

    /// <inheritdoc />
    public override AstNode Clone() =>
        new RustFunctionNode(
            (IdentifierNode)Name.Clone(),
            Parameters.Select(p => new RustParameter(p.Name, (TypeNode)p.Type.Clone())).ToImmutableList(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            (BlockNode)Body.Clone(),
            IsAsync,
            Visibility,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary(),
            (ExpressionNode?)Tail?.Clone());
}
