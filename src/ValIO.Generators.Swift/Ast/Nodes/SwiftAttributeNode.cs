using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>
/// Swift attribute reference: <c>@discardableResult</c>, <c>@available(iOS 13, *)</c>, etc.
/// Arguments are pre-rendered Swift expressions to keep the AST minimal.
/// </summary>
public sealed class SwiftAttributeNode : AstNode
{
    /// <summary>Attribute name (without leading <c>@</c>), e.g. <c>discardableResult</c>.</summary>
    public string Name { get; }

    /// <summary>Argument literals (pre-rendered).</summary>
    public IReadOnlyList<string> Arguments { get; }

    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();

    public SwiftAttributeNode(
        string name,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Attribute name cannot be null or whitespace", nameof(name));
        Name = name;
        Arguments = arguments?.ToImmutableList() ?? ImmutableList<string>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ISwiftAstVisitor<TResult> swiftVisitor)
            return swiftVisitor.VisitSwiftAttribute(this);
        throw new NotSupportedException("Visitor must implement ISwiftAstVisitor<TResult> to visit SwiftAttributeNode");
    }

    public override AstNode Clone() =>
        new SwiftAttributeNode(Name, Arguments.ToImmutableList(), Metadata.ToImmutableDictionary());
}
