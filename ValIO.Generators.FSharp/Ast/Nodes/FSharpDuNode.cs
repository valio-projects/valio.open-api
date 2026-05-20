using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Nodes;

/// <summary>
/// F# discriminated union:
///   <c>type Name = | Case1 of payload | Case2</c>
/// </summary>
public sealed class FSharpDuNode : DeclarationNode
{
    /// <summary>Single DU case (case name + optional payload type).</summary>
    public sealed record Case(string Name, TypeNode? Payload);

    /// <summary>Ordered list of cases.</summary>
    public IReadOnlyList<Case> Cases { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            foreach (var c in Cases)
                if (c.Payload != null) children.Add(c.Payload);
            return children;
        }
    }

    public FSharpDuNode(
        IdentifierNode name,
        IReadOnlyList<Case>? cases = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Cases = cases?.ToImmutableList() ?? ImmutableList<Case>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IFSharpAstVisitor<TResult> fsVisitor)
            return fsVisitor.VisitFSharpDU(this);
        throw new NotSupportedException("Visitor must implement IFSharpAstVisitor<TResult> to visit FSharpDuNode");
    }

    public override AstNode Clone() =>
        new FSharpDuNode(
            (IdentifierNode)Name.Clone(),
            Cases.Select(c => new Case(
                c.Name,
                c.Payload != null ? (TypeNode)c.Payload.Clone() : null)).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
