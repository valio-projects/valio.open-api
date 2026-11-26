using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Nodes;

/// <summary>Declares a local binding with an optional type and mutable access.</summary>
/// <param name="name">The binding identifier.</param>
/// <param name="initializer">The initial value.</param>
/// <param name="type">The explicit type if present.</param>
/// <param name="mutable">The mutability of the binding.</param>
public sealed class RustLetNode(IdentifierNode name, ExpressionNode initializer, TypeNode? type, bool mutable) : StatementNode
{
    /// <summary>Gets the binding identifier.</summary>
    /// <value>The binding identifier.</value>
    public IdentifierNode Name { get; } = name;

    /// <summary>Gets the initial value.</summary>
    /// <value>The initial value.</value>
    public ExpressionNode Initializer { get; } = initializer;

    /// <summary>Gets the explicit type if present.</summary>
    /// <value>The explicit type if present.</value>
    public TypeNode? Type { get; } = type;

    /// <summary>Gets the mutability of the binding.</summary>
    /// <value>The mutability of the binding.</value>
    public bool Mutable { get; } = mutable;

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children => Type is null ? [Name, Initializer] : [Name, Type, Initializer];

    /// <inheritdoc />
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IRustAstVisitor<TResult> rust ? rust.VisitRustLet(this) : visitor.DefaultVisit(this);

    /// <inheritdoc />
    public override AstNode Clone() => new RustLetNode((IdentifierNode)Name.Clone(), (ExpressionNode)Initializer.Clone(),
        (TypeNode?)Type?.Clone(), Mutable);
}
