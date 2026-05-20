using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>
/// Rust function parameter (<c>name: Type</c>).
/// </summary>
public sealed record RustParameter(string Name, TypeNode Type);

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
            Parameters.Select(p => p with { Type = (TypeNode)p.Type.Clone() }).ToImmutableList(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            Body.ToImmutableList(),
            IsAsync,
            Visibility,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
