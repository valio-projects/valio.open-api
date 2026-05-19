using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Php.Ast.Visitors;

namespace ValIO.Generators.Php.Ast.Nodes;

/// <summary>
/// PHP <c>use</c>-секция, например: <c>use GuzzleHttp\ClientInterface;</c> или <c>use GuzzleHttp\ClientInterface as Client;</c>.
/// </summary>
public sealed class PhpUseStatementNode : AstNode
{
    public string FullyQualifiedName { get; }
    public string? Alias { get; }

    public override IReadOnlyList<AstNode> Children => ImmutableList<AstNode>.Empty;

    public PhpUseStatementNode(string fullyQualifiedName, string? alias = null, IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        FullyQualifiedName = fullyQualifiedName?.TrimStart('\\') ?? throw new ArgumentNullException(nameof(fullyQualifiedName));
        Alias = alias;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPhpAstVisitor<TResult> phpVisitor)
            return phpVisitor.VisitPhpUseStatement(this);
        throw new NotSupportedException($"Visitor must implement IPhpAstVisitor<TResult> to visit {nameof(PhpUseStatementNode)}.");
    }

    public override AstNode Clone() => new PhpUseStatementNode(FullyQualifiedName, Alias, Metadata.ToImmutableDictionary());
}
