using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Nodes;

/// <summary>
/// F# record type:
///   <c>type Name = {{ Field1: T1; Field2: T2 }}</c>
/// </summary>
public sealed class FSharpRecordNode : DeclarationNode
{
    /// <summary>Record field (label + type).</summary>
    public sealed record Field(string Name, TypeNode Type);

    /// <summary>Ordered list of record fields.</summary>
    public IReadOnlyList<Field> Fields { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            foreach (var f in Fields)
                children.Add(f.Type);
            return children;
        }
    }

    public FSharpRecordNode(
        IdentifierNode name,
        IReadOnlyList<Field>? fields = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Fields = fields?.ToImmutableList() ?? ImmutableList<Field>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IFSharpAstVisitor<TResult> fsVisitor)
            return fsVisitor.VisitFSharpRecord(this);
        throw new NotSupportedException("Visitor must implement IFSharpAstVisitor<TResult> to visit FSharpRecordNode");
    }

    public override AstNode Clone() =>
        new FSharpRecordNode(
            (IdentifierNode)Name.Clone(),
            Fields.Select(f => new Field(f.Name, (TypeNode)f.Type.Clone())).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
