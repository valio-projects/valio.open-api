using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Ruby.Ast.Visitors;

namespace ValIO.Generators.Ruby.Ast.Nodes;

/// <summary>Represents a Ruby method and its structured arguments and statements.</summary>
public sealed class RubyMethodNode : DeclarationNode
{
    /// <summary>Gets the ordered parameter declarations.</summary>
    /// <value>Positional and keyword parameters.</value>
    public IReadOnlyList<ParameterDeclarationNode> Parameters { get; }

    /// <summary>Gets the method's statements.</summary>
    /// <value>The ordered statements in the body.</value>
    public BlockNode Body { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children =>
        Documentation is null ? [Name, .. Parameters, Body] : [Name, Documentation, .. Parameters, Body];

    /// <summary>Creates a Ruby method declaration.</summary>
    /// <param name="name">The method identifier.</param>
    /// <param name="parameters">The optional formal parameters.</param>
    /// <param name="body">The body, or null for an empty method.</param>
    /// <param name="documentation">The optional method documentation.</param>
    /// <param name="metadata">The associated metadata.</param>
    public RubyMethodNode(
        IdentifierNode name,
        IReadOnlyList<ParameterDeclarationNode>? parameters = null,
        BlockNode? body = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<ParameterDeclarationNode>.Empty;
        Body = body ?? new BlockNode();
    }

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IRubyAstVisitor<TResult> ruby ? ruby.VisitRubyMethod(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new RubyMethodNode(
        (IdentifierNode)Name.Clone(), Parameters.Select(parameter => (ParameterDeclarationNode)parameter.Clone()).ToArray(),
        (BlockNode)Body.Clone(), (CommentNode?)Documentation?.Clone(), Metadata);
}
