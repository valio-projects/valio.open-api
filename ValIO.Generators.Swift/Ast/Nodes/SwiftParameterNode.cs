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
    public IdentifierNode Name { get; }
    public TypeNode Type { get; }
    public string? DefaultValueLiteral { get; }

    /// <summary>
    /// Optional external argument label. Pass "_" to suppress the label entirely.
    /// </summary>
    public string? ExternalLabel { get; }

    public override IReadOnlyList<AstNode> Children => new AstNode[] { Name, Type };

    public SwiftParameterNode(
        IdentifierNode name,
        TypeNode type,
        string? defaultValueLiteral = null,
        string? externalLabel = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        DefaultValueLiteral = defaultValueLiteral;
        ExternalLabel = externalLabel;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ISwiftAstVisitor<TResult> swiftVisitor)
            return swiftVisitor.VisitSwiftParameter(this);
        throw new NotSupportedException("Visitor must implement ISwiftAstVisitor<TResult> to visit SwiftParameterNode");
    }

    public override AstNode Clone() =>
        new SwiftParameterNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            DefaultValueLiteral,
            ExternalLabel,
            Metadata.ToImmutableDictionary());
}
