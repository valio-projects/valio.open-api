using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Php.Ast.Visitors;

namespace ValIO.Generators.Php.Ast.Nodes;

/// <summary>
/// PHP <c>__construct</c> с поддержкой constructor property promotion.
/// </summary>
public sealed class PhpConstructorDeclarationNode : DeclarationNode
{
    public IReadOnlyList<PhpPromotedParameter> PromotedParameters { get; }
    public BlockNode? Body { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var c = new List<AstNode> { Name };
            if (Documentation != null) c.Add(Documentation);
            c.AddRange(Attributes);
            foreach (var p in PromotedParameters)
                c.Add(p.Parameter);
            if (Body != null) c.Add(Body);
            return c;
        }
    }

    public PhpConstructorDeclarationNode(
        IReadOnlyList<PhpPromotedParameter>? promotedParameters = null,
        BlockNode? body = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(new IdentifierNode("__construct"), documentation, attributes, metadata)
    {
        PromotedParameters = promotedParameters?.ToImmutableList() ?? ImmutableList<PhpPromotedParameter>.Empty;
        Body = body;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPhpAstVisitor<TResult> phpVisitor)
            return phpVisitor.VisitPhpConstructor(this);
        throw new NotSupportedException($"Visitor must implement IPhpAstVisitor<TResult> to visit {nameof(PhpConstructorDeclarationNode)}.");
    }

    public override AstNode Clone() => new PhpConstructorDeclarationNode(
        PromotedParameters.Select(p => p.Clone()).ToImmutableList(),
        Body != null ? (BlockNode)Body.Clone() : null,
        Documentation != null ? (CommentNode)Documentation.Clone() : null,
        Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
        Metadata.ToImmutableDictionary());
}

/// <summary>
/// Promoted constructor parameter: задаёт modifier (<c>public/private/protected</c>), <c>readonly</c>-флаг,
/// сам <see cref="ParameterDeclarationNode"/> и опциональный PHPDoc-тег.
/// </summary>
public sealed class PhpPromotedParameter
{
    public string Visibility { get; }
    public bool IsReadonly { get; }
    public ParameterDeclarationNode Parameter { get; }
    public string? PhpDocType { get; }

    public PhpPromotedParameter(
        string visibility,
        bool isReadonly,
        ParameterDeclarationNode parameter,
        string? phpDocType = null)
    {
        Visibility = visibility;
        IsReadonly = isReadonly;
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        PhpDocType = phpDocType;
    }

    public PhpPromotedParameter Clone() => new PhpPromotedParameter(
        Visibility,
        IsReadonly,
        (ParameterDeclarationNode)Parameter.Clone(),
        PhpDocType);
}
