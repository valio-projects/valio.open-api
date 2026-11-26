using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>Declares a constant or mutable variable, with tuple destructuring when needed.</summary>
/// <param name="names">The bound identifiers.</param>
/// <param name="initializer">The initial value.</param>
/// <param name="mutable">The mutability of the binding.</param>
/// <param name="type">The optional explicit binding type.</param>
public sealed class SwiftBindingNode(IReadOnlyList<IdentifierNode> names, ExpressionNode initializer, bool mutable, TypeNode? type) : StatementNode
{
    /// <summary>Gets the bound identifiers.</summary>
    /// <value>The bound identifiers.</value>
    public IReadOnlyList<IdentifierNode> Names { get; } = names.ToArray();

    /// <summary>Gets the initial value.</summary>
    /// <value>The initial value.</value>
    public ExpressionNode Initializer { get; } = initializer;

    /// <summary>Gets the mutability of the binding.</summary>
    /// <value>The mutability of the binding.</value>
    public bool Mutable { get; } = mutable;

    /// <summary>Gets the optional explicit binding type.</summary>
    /// <value>The optional explicit binding type.</value>
    public TypeNode? Type { get; } = type;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => Type is null ? [.. Names, Initializer] : [.. Names, Type, Initializer];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ISwiftAstVisitor<TResult> swift ? swift.VisitSwiftBinding(this) : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new SwiftBindingNode(Names.Select(name => (IdentifierNode)name.Clone()).ToArray(), (ExpressionNode)Initializer.Clone(), Mutable, (TypeNode?)Type?.Clone());
}
