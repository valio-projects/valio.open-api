using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Nodes;

/// <summary>
/// F# function binding:
///   <c>let name (p1: T1) (p2: T2) : R =\n    body</c>
/// Body lines are pre-rendered strings to keep the AST minimal — they live on the
/// AST node itself and only the renderer's Visit* methods emit text.
/// </summary>
public sealed class FSharpFunctionNode : DeclarationNode
{
    /// <summary>Parameter (name + type annotation).</summary>
    public sealed record Parameter(string Name, TypeNode Type);

    /// <summary>Ordered list of parameters.</summary>
    public IReadOnlyList<Parameter> Parameters { get; }

    /// <summary>Annotated return type. Null = inferred / unit (renderer omits the annotation).</summary>
    public TypeNode? ReturnType { get; }

    /// <summary>Body lines (pre-rendered, without outer indent).</summary>
    public IReadOnlyList<string> BodyLines { get; }

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

    public FSharpFunctionNode(
        IdentifierNode name,
        IReadOnlyList<Parameter>? parameters = null,
        TypeNode? returnType = null,
        IReadOnlyList<string>? bodyLines = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<Parameter>.Empty;
        ReturnType = returnType;
        BodyLines = bodyLines?.ToImmutableList() ?? ImmutableList<string>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IFSharpAstVisitor<TResult> fsVisitor)
            return fsVisitor.VisitFSharpFunction(this);
        throw new NotSupportedException("Visitor must implement IFSharpAstVisitor<TResult> to visit FSharpFunctionNode");
    }

    public override AstNode Clone() =>
        new FSharpFunctionNode(
            (IdentifierNode)Name.Clone(),
            Parameters.Select(p => new Parameter(p.Name, (TypeNode)p.Type.Clone())).ToImmutableList(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            BodyLines.ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
