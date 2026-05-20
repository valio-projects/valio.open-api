using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Nodes;

/// <summary>
/// A Dart enum with a backing string/int value per member.
/// </summary>
public sealed class DartEnumDeclarationNode : DeclarationNode
{
    public IReadOnlyList<DartEnumMemberNode> Members { get; }
    /// <summary>Backing-value type for the optional <c>final TBacking value;</c> field.</summary>
    public TypeNode? BackingType { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Insert(0, Documentation);
            children.AddRange(Members);
            return children;
        }
    }

    public DartEnumDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<DartEnumMemberNode>? members = null,
        TypeNode? backingType = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Members = members?.ToImmutableList() ?? ImmutableList<DartEnumMemberNode>.Empty;
        BackingType = backingType;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IDartAstVisitor<TResult> dartVisitor)
            return dartVisitor.VisitDartEnum(this);
        throw new NotSupportedException("Visitor must implement IDartAstVisitor<TResult> to visit DartEnumDeclarationNode");
    }

    public override AstNode Clone() =>
        new DartEnumDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Members.Select(m => (DartEnumMemberNode)m.Clone()).ToImmutableList(),
            BackingType != null ? (TypeNode)BackingType.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
