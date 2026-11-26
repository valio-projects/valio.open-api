using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>
/// Swift function parameter:
///   <c>[externalLabel] name: Type [= default]</c>
/// When <see cref="ExternalLabel"/> is null the label equals the parameter name.
/// </summary>
public sealed class SwiftParameterNode : AstNode
{
    /// <summary>Gets the local parameter name.</summary>
    /// <value>The identifier visible inside the function.</value>
    public IdentifierNode Name { get; }

    /// <summary>Gets the declared Swift type.</summary>
    /// <value>The formal parameter annotation.</value>
    public TypeNode Type { get; }

    /// <summary>Gets the initializer for an omitted argument.</summary>
    /// <value>The default expression when present.</value>
    public ExpressionNode? DefaultValue { get; }

    /// <summary>
    /// Optional external argument label. Pass "_" to suppress the label entirely.
    /// </summary>
    public string? ExternalLabel { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => DefaultValue is null ? [Name, Type] : [Name, Type, DefaultValue];

    /// <summary>Creates a Swift parameter with a typed annotation and optional default.</summary>
    /// <param name="name">The local identifier.</param>
    /// <param name="type">The declared Swift type.</param>
    /// <param name="defaultValue">The default expression.</param>
    /// <param name="externalLabel">The argument label when it differs from the local name.</param>
    /// <param name="metadata">The associated AST metadata.</param>
    public SwiftParameterNode(
        IdentifierNode name,
        TypeNode type,
        ExpressionNode? defaultValue = null,
        string? externalLabel = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        DefaultValue = defaultValue;
        ExternalLabel = externalLabel;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ISwiftAstVisitor<TResult> swiftVisitor)
            return swiftVisitor.VisitSwiftParameter(this);
        throw new NotSupportedException("Visitor must implement ISwiftAstVisitor<TResult> to visit SwiftParameterNode");
    }

    /// <inheritdoc/>
    public override AstNode Clone() =>
        new SwiftParameterNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            (ExpressionNode?)DefaultValue?.Clone(),
            ExternalLabel,
            Metadata.ToImmutableDictionary());
}
