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
/// Body lines are pre-rendered strings (kept minimal — full expression AST is
/// not warranted for this MVP).
/// </summary>
public sealed class RustFunctionNode : DeclarationNode
{
    /// <summary>Parameters.</summary>
    public IReadOnlyList<RustParameter> Parameters { get; }

    /// <summary>Return type. Null = unit (omitted in source).</summary>
    public TypeNode? ReturnType { get; }

    /// <summary>Body lines (pre-rendered, without outer indent).</summary>
    public IReadOnlyList<string> Body { get; }

    /// <summary>True for <c>async fn</c>.</summary>
    public bool IsAsync { get; }

    /// <summary>Visibility (default <c>pub</c>).</summary>
    public string Visibility { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            foreach (var p in Parameters)
                children.Add(p.Type);
            if (ReturnType != null) children.Add(ReturnType);
            return children;
        }
    }

    public RustFunctionNode(
        IdentifierNode name,
        IReadOnlyList<RustParameter>? parameters = null,
        TypeNode? returnType = null,
        IReadOnlyList<string>? body = null,
        bool isAsync = true,
        string visibility = "pub",
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<RustParameter>.Empty;
        ReturnType = returnType;
        Body = body?.ToImmutableList() ?? ImmutableList<string>.Empty;
        IsAsync = isAsync;
        Visibility = visibility;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IRustAstVisitor<TResult> rustVisitor)
            return rustVisitor.VisitRustFunction(this);
        throw new NotSupportedException("Visitor must implement IRustAstVisitor<TResult> to visit RustFunctionNode");
    }

    public override AstNode Clone() =>
        new RustFunctionNode(
            (IdentifierNode)Name.Clone(),
            Parameters.Select(p => new RustParameter(p.Name, (TypeNode)p.Type.Clone())).ToImmutableList(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            Body.ToImmutableList(),
            IsAsync,
            Visibility,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
