using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Nodes;

/// <summary>
/// A Dart class with fields, optional constructor, and methods.
/// </summary>
public sealed class DartClassDeclarationNode : DeclarationNode
{
    public IReadOnlyList<DartFieldNode> Fields { get; }
    public IReadOnlyList<DartMethodNode> Methods { get; }
    public TypeNode? BaseType { get; }
    public IReadOnlyList<TypeNode> Mixins { get; }
    public bool IsAbstract { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Insert(0, Documentation);
            if (BaseType != null) children.Add(BaseType);
            children.AddRange(Mixins);
            children.AddRange(Fields);
            children.AddRange(Methods);
            return children;
        }
    }

    public DartClassDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<DartFieldNode>? fields = null,
        IReadOnlyList<DartMethodNode>? methods = null,
        TypeNode? baseType = null,
        IReadOnlyList<TypeNode>? mixins = null,
        bool isAbstract = false,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Fields = fields?.ToImmutableList() ?? ImmutableList<DartFieldNode>.Empty;
        Methods = methods?.ToImmutableList() ?? ImmutableList<DartMethodNode>.Empty;
        BaseType = baseType;
        Mixins = mixins?.ToImmutableList() ?? ImmutableList<TypeNode>.Empty;
        IsAbstract = isAbstract;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IDartAstVisitor<TResult> dartVisitor)
            return dartVisitor.VisitDartClass(this);
        throw new NotSupportedException("Visitor must implement IDartAstVisitor<TResult> to visit DartClassDeclarationNode");
    }

    public override AstNode Clone() =>
        new DartClassDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Fields.Select(f => (DartFieldNode)f.Clone()).ToImmutableList(),
            Methods.Select(m => (DartMethodNode)m.Clone()).ToImmutableList(),
            BaseType != null ? (TypeNode)BaseType.Clone() : null,
            Mixins.Select(m => (TypeNode)m.Clone()).ToImmutableList(),
            IsAbstract,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
