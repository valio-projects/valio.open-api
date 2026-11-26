using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Nodes;

/// <summary>
/// Represents a Dart method with a structured body or a bodyless constructor.
/// </summary>
public sealed class DartMethodNode : DeclarationNode
{
    /// <summary>Gets the declared result type of the method.</summary>
    /// <value>The result type when present.</value>
    public TypeNode? ReturnType { get; }

    /// <summary>Gets the formal parameters in declaration order.</summary>
    /// <value>The ordered parameters.</value>
    public IReadOnlyList<DartParameterNode> Parameters { get; }

    /// <summary>Gets the method's executable block.</summary>
    /// <value>The statements or null for a bodyless declaration.</value>
    public BlockNode? Body { get; }

    /// <summary>Gets whether the method awaits asynchronous work.</summary>
    /// <value>Whether to render the async modifier.</value>
    public bool IsAsync { get; }

    /// <summary>Gets whether the method belongs to the type itself.</summary>
    /// <value>Whether to render the static modifier.</value>
    public bool IsStatic { get; }

    /// <summary>Gets whether the member creates an instance of its containing type.</summary>
    /// <value>Whether to omit the return type and use a constructor signature.</value>
    public bool IsConstructor { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Insert(0, Documentation);
            if (ReturnType != null) children.Add(ReturnType);
            children.AddRange(Parameters);
            if (Body is not null) children.Add(Body);
            return children;
        }
    }

    /// <summary>Creates a method or constructor declaration.</summary>
    /// <param name="name">The declared member name.</param>
    /// <param name="returnType">The method result type, if supplied.</param>
    /// <param name="parameters">The formal parameters in order.</param>
    /// <param name="body">The statements of the member, if present.</param>
    /// <param name="isAsync">Whether execution is asynchronous.</param>
    /// <param name="isStatic">Whether this is a static method.</param>
    /// <param name="isConstructor">Whether the member constructs its containing type.</param>
    /// <param name="documentation">The optional Dart documentation.</param>
    /// <param name="metadata">Metadata copied into the declaration.</param>
    public DartMethodNode(
        IdentifierNode name,
        TypeNode? returnType = null,
        IReadOnlyList<DartParameterNode>? parameters = null,
        BlockNode? body = null,
        bool isAsync = false,
        bool isStatic = false,
        bool isConstructor = false,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        ReturnType = returnType;
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<DartParameterNode>.Empty;
        Body = body;
        IsAsync = isAsync;
        IsStatic = isStatic;
        IsConstructor = isConstructor;
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">The visitor cannot process Dart declarations.</exception>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IDartAstVisitor<TResult> dartVisitor)
            return dartVisitor.VisitDartMethod(this);
        throw new NotSupportedException("Visitor must implement IDartAstVisitor<TResult> to visit DartMethodNode");
    }

    /// <inheritdoc/>
    public override AstNode Clone() =>
        new DartMethodNode(
            (IdentifierNode)Name.Clone(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            Parameters.Select(p => (DartParameterNode)p.Clone()).ToImmutableList(),
            (BlockNode?)Body?.Clone(),
            IsAsync,
            IsStatic,
            IsConstructor,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
