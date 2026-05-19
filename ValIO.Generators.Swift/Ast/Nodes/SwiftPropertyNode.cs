using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>
/// Swift property declaration: <c>[attrs] [access] let|var name: Type [= default]</c>.
/// Also used for struct stored properties and for class stored properties.
/// </summary>
public sealed class SwiftPropertyNode : DeclarationNode
{
    /// <summary>Property type annotation.</summary>
    public TypeNode Type { get; }

    /// <summary>Default value literal (pre-rendered) or null when none.</summary>
    public string? DefaultValueLiteral { get; }

    /// <summary>If true, declared with <c>let</c>; otherwise <c>var</c>.</summary>
    public bool IsConstant { get; }

    /// <summary>Access modifier (e.g. <c>public</c>, <c>private</c>, <c>internal</c>). Null = default internal.</summary>
    public string? AccessModifier { get; }

    /// <summary>True if the property is rendered with the <c>static</c> keyword.</summary>
    public bool IsStatic { get; }

    /// <summary>Swift-specific attributes attached to the property.</summary>
    public IReadOnlyList<SwiftAttributeNode> SwiftAttributes { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, Type };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(SwiftAttributes);
            return children;
        }
    }

    public SwiftPropertyNode(
        IdentifierNode name,
        TypeNode type,
        string? defaultValueLiteral = null,
        bool isConstant = true,
        string? accessModifier = null,
        bool isStatic = false,
        IReadOnlyList<SwiftAttributeNode>? attributes = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        DefaultValueLiteral = defaultValueLiteral;
        IsConstant = isConstant;
        AccessModifier = accessModifier;
        IsStatic = isStatic;
        SwiftAttributes = attributes?.ToImmutableList() ?? ImmutableList<SwiftAttributeNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ISwiftAstVisitor<TResult> swiftVisitor)
            return swiftVisitor.VisitSwiftProperty(this);
        throw new NotSupportedException("Visitor must implement ISwiftAstVisitor<TResult> to visit SwiftPropertyNode");
    }

    public override AstNode Clone() =>
        new SwiftPropertyNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            DefaultValueLiteral,
            IsConstant,
            AccessModifier,
            IsStatic,
            SwiftAttributes.Select(a => (SwiftAttributeNode)a.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
