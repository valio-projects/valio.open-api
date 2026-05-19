using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Visitors;

namespace ValIO.Generators.Python.Ast.Nodes;

/// <summary>
/// Член Python-enum: <c>NAME = "value"</c> или <c>NAME = 1</c>.
/// </summary>
public sealed class PythonEnumMemberNode : AstNode
{
    /// <summary>Имя члена (SCREAMING_SNAKE_CASE).</summary>
    public string Name { get; }

    /// <summary>Литерал значения (например <c>"admin"</c> или <c>1</c>).</summary>
    public string ValueLiteral { get; }

    public CommentNode? Documentation { get; }

    public override IReadOnlyList<AstNode> Children =>
        Documentation != null ? new[] { (AstNode)Documentation } : Array.Empty<AstNode>();

    public PythonEnumMemberNode(
        string name,
        string valueLiteral,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enum member name required", nameof(name));
        Name = name;
        ValueLiteral = valueLiteral ?? throw new ArgumentNullException(nameof(valueLiteral));
        Documentation = documentation;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPythonAstVisitor<TResult> pyVisitor)
            return pyVisitor.VisitPythonEnumMember(this);
        throw new NotSupportedException("Visitor must implement IPythonAstVisitor<TResult> to visit PythonEnumMemberNode");
    }

    public override AstNode Clone() =>
        new PythonEnumMemberNode(
            Name,
            ValueLiteral,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
