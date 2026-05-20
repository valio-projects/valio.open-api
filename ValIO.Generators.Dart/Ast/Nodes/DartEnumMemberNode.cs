using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Nodes;

/// <summary>
/// One value of a Dart enum: <c>active</c> or <c>active('ACT')</c>.
/// </summary>
public sealed class DartEnumMemberNode : AstNode
{
    public IdentifierNode Name { get; }
    public string? ValueLiteral { get; }

    public override IReadOnlyList<AstNode> Children => new AstNode[] { Name };

    public DartEnumMemberNode(
        IdentifierNode name,
        string? valueLiteral = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ValueLiteral = valueLiteral;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IDartAstVisitor<TResult> dartVisitor)
            return dartVisitor.VisitDartEnumMember(this);
        throw new NotSupportedException("Visitor must implement IDartAstVisitor<TResult> to visit DartEnumMemberNode");
    }

    public override AstNode Clone() =>
        new DartEnumMemberNode(
            (IdentifierNode)Name.Clone(),
            ValueLiteral,
            Metadata.ToImmutableDictionary());
}
