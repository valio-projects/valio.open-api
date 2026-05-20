using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Nodes;

/// <summary>
/// A Dart method or top-level function. Body lines are stored verbatim
/// — builders compose them once via small helpers; the renderer indents
/// and joins them.
/// </summary>
public sealed class DartMethodNode : DeclarationNode
{
    public TypeNode? ReturnType { get; }
    public IReadOnlyList<DartParameterNode> Parameters { get; }
    public IReadOnlyList<string> BodyLines { get; }
    public bool IsAsync { get; }
    public bool IsStatic { get; }
    public bool IsConstructor { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Insert(0, Documentation);
            if (ReturnType != null) children.Add(ReturnType);
            children.AddRange(Parameters);
            return children;
        }
    }

    public DartMethodNode(
        IdentifierNode name,
        TypeNode? returnType = null,
        IReadOnlyList<DartParameterNode>? parameters = null,
        IReadOnlyList<string>? bodyLines = null,
        bool isAsync = false,
        bool isStatic = false,
        bool isConstructor = false,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        ReturnType = returnType;
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<DartParameterNode>.Empty;
        BodyLines = bodyLines?.ToImmutableList() ?? ImmutableList<string>.Empty;
        IsAsync = isAsync;
        IsStatic = isStatic;
        IsConstructor = isConstructor;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IDartAstVisitor<TResult> dartVisitor)
            return dartVisitor.VisitDartMethod(this);
        throw new NotSupportedException("Visitor must implement IDartAstVisitor<TResult> to visit DartMethodNode");
    }

    public override AstNode Clone() =>
        new DartMethodNode(
            (IdentifierNode)Name.Clone(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            Parameters.Select(p => (DartParameterNode)p.Clone()).ToImmutableList(),
            BodyLines.ToImmutableList(),
            IsAsync,
            IsStatic,
            IsConstructor,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
