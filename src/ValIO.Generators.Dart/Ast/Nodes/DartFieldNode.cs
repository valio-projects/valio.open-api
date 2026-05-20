using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Nodes;

/// <summary>
/// A Dart class field: <c>final String name;</c> (immutable by convention).
/// </summary>
public sealed class DartFieldNode : DeclarationNode
{
    public TypeNode Type { get; }
    public bool IsNullable { get; }
    public bool IsFinal { get; }
    /// <summary>
    /// Optional <c>@JsonKey(name: "...")</c> annotation argument when the
    /// JSON key differs from the Dart field name.
    /// </summary>
    public string? JsonKey { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, Type };
            if (Documentation != null) children.Insert(0, Documentation);
            return children;
        }
    }

    public DartFieldNode(
        IdentifierNode name,
        TypeNode type,
        bool isNullable = false,
        bool isFinal = true,
        string? jsonKey = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsNullable = isNullable;
        IsFinal = isFinal;
        JsonKey = jsonKey;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IDartAstVisitor<TResult> dartVisitor)
            return dartVisitor.VisitDartField(this);
        throw new NotSupportedException("Visitor must implement IDartAstVisitor<TResult> to visit DartFieldNode");
    }

    public override AstNode Clone() =>
        new DartFieldNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            IsNullable,
            IsFinal,
            JsonKey,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
