using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Visitors;

namespace ValIO.Generators.Python.Ast.Nodes;

/// <summary>
/// Объявление Python-класса.
/// Members могут быть PythonAttributeAssignmentNode (поля с аннотациями), PythonFunctionDeclarationNode (методы).
/// </summary>
public sealed class PythonClassDeclarationNode : DeclarationNode
{
    /// <summary>Базовые классы (BaseModel, StrEnum и т.д.).</summary>
    public IReadOnlyList<TypeNode> BaseClasses { get; }

    /// <summary>Члены класса (атрибуты, методы, вложенные классы).</summary>
    public IReadOnlyList<DeclarationNode> Members { get; }

    /// <summary>Декораторы класса.</summary>
    public IReadOnlyList<string> Decorators { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(BaseClasses);
            children.AddRange(Members);
            return children;
        }
    }

    public PythonClassDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<TypeNode>? baseClasses = null,
        IReadOnlyList<DeclarationNode>? members = null,
        IReadOnlyList<string>? decorators = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        BaseClasses = baseClasses?.ToImmutableList() ?? ImmutableList<TypeNode>.Empty;
        Members = members?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
        Decorators = decorators?.ToImmutableList() ?? ImmutableList<string>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPythonAstVisitor<TResult> pyVisitor)
            return pyVisitor.VisitPythonClassDeclaration(this);
        throw new NotSupportedException("Visitor must implement IPythonAstVisitor<TResult> to visit PythonClassDeclarationNode");
    }

    public override AstNode Clone() =>
        new PythonClassDeclarationNode(
            (IdentifierNode)Name.Clone(),
            BaseClasses.Select(b => (TypeNode)b.Clone()).ToImmutableList(),
            Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
            Decorators.ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
