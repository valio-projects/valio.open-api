using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Visitors;

namespace ValIO.Generators.Python.Ast.Nodes;

/// <summary>
/// Тип базового класса для Python enum: <c>StrEnum</c> или <c>IntEnum</c>.
/// </summary>
public enum PythonEnumBase
{
    StrEnum,
    IntEnum
}

/// <summary>
/// Объявление Python-enum (StrEnum / IntEnum).
/// </summary>
public sealed class PythonEnumDeclarationNode : DeclarationNode
{
    public PythonEnumBase BaseClass { get; }
    public IReadOnlyList<PythonEnumMemberNode> Members { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(Members);
            return children;
        }
    }

    public PythonEnumDeclarationNode(
        IdentifierNode name,
        PythonEnumBase baseClass,
        IReadOnlyList<PythonEnumMemberNode>? members = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        BaseClass = baseClass;
        Members = members?.ToImmutableList() ?? ImmutableList<PythonEnumMemberNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPythonAstVisitor<TResult> pyVisitor)
            return pyVisitor.VisitPythonEnumDeclaration(this);
        throw new NotSupportedException("Visitor must implement IPythonAstVisitor<TResult> to visit PythonEnumDeclarationNode");
    }

    public override AstNode Clone() =>
        new PythonEnumDeclarationNode(
            (IdentifierNode)Name.Clone(),
            BaseClass,
            Members.Select(m => (PythonEnumMemberNode)m.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
