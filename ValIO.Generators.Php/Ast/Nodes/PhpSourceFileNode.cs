using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Php.Ast.Visitors;

namespace ValIO.Generators.Php.Ast.Nodes;

/// <summary>
/// PHP исходный файл: <c>&lt;?php declare(strict_types=1);</c>, namespace, use-секции и декларации.
/// </summary>
public sealed class PhpSourceFileNode : AstNode
{
    public string? Namespace { get; }
    public IReadOnlyList<PhpUseStatementNode> Uses { get; }
    public IReadOnlyList<DeclarationNode> Declarations { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var c = new List<AstNode>();
            c.AddRange(Uses);
            c.AddRange(Declarations);
            return c;
        }
    }

    public PhpSourceFileNode(
        string? @namespace = null,
        IReadOnlyList<PhpUseStatementNode>? uses = null,
        IReadOnlyList<DeclarationNode>? declarations = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Namespace = @namespace;
        Uses = uses?.ToImmutableList() ?? ImmutableList<PhpUseStatementNode>.Empty;
        Declarations = declarations?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPhpAstVisitor<TResult> phpVisitor)
            return phpVisitor.VisitPhpSourceFile(this);
        throw new NotSupportedException($"Visitor must implement IPhpAstVisitor<TResult> to visit {nameof(PhpSourceFileNode)}.");
    }

    public override AstNode Clone() => new PhpSourceFileNode(
        Namespace,
        Uses.Select(u => (PhpUseStatementNode)u.Clone()).ToImmutableList(),
        Declarations.Select(d => (DeclarationNode)d.Clone()).ToImmutableList(),
        Metadata.ToImmutableDictionary());
}
