using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Renderers;

/// <summary>
/// Chain-of-Responsibility link that lets <see cref="TypeScriptAstRenderer"/> emit
/// TanStack Query AST nodes (<see cref="UseQueryNode"/>, <see cref="UseMutationNode"/>,
/// <see cref="QueryKeyNode"/>, <see cref="QueryOptionsNode"/>,
/// <see cref="MutationOptionsNode"/>) when they appear in a generic source file.
///
/// These node types <c>Accept</c> their TanStack-aware visitor first and fall through
/// to <c>DefaultVisit</c> otherwise — that's the path this renderer claims.
/// </summary>
public sealed class TanStackNodeRenderer : ITypeScriptNodeRenderer
{
    /// <inheritdoc />
    public int Priority => 90;

    /// <inheritdoc />
    public bool CanRender(AstNode node) =>
        node is UseQueryNode
            or UseMutationNode
            or QueryKeyNode
            or QueryOptionsNode
            or MutationOptionsNode;

    /// <inheritdoc />
    public string Render(AstNode node, ITypeScriptAstRenderer host)
    {
        var renderer = new TanStackQueryAstRenderer();
        return node switch
        {
            UseQueryNode q => renderer.VisitUseQuery(q),
            UseMutationNode m => renderer.VisitUseMutation(m),
            QueryKeyNode key => renderer.VisitQueryKey(key),
            QueryOptionsNode opts => renderer.VisitQueryOptions(opts),
            MutationOptionsNode opts => renderer.VisitMutationOptions(opts),
            _ => throw new NotSupportedException(
                $"{nameof(TanStackNodeRenderer)} cannot render {node.GetType().Name}"),
        };
    }
}
