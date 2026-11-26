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
    /// <summary>Gets the optional wire-value expression.</summary>
    public ExpressionNode? Value { get; }

    public override IReadOnlyList<AstNode> Children => Value is null ? [Name] : [Name, Value];

    public DartEnumMemberNode(
        IdentifierNode name,
        ExpressionNode? value = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;
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
            (ExpressionNode?)Value?.Clone(),
            Metadata.ToImmutableDictionary());
}
