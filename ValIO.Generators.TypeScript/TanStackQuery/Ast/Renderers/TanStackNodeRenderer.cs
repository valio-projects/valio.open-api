using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Renderers;

/// <summary>
/// Звено цепочки ответственности для TanStack Query-узлов
/// (<see cref="UseQueryNode"/>, <see cref="UseMutationNode"/>,
/// <see cref="QueryKeyNode"/>, <see cref="QueryOptionsNode"/>,
/// <see cref="MutationOptionsNode"/>).
/// </summary>
/// <remarks>
/// Приоритет 90 — ниже валидаторов (Zod=100), чтобы Z*Node-узлы внутри hooks
/// рендерились звеном валидатора, а не TanStack.
/// </remarks>
public sealed class TanStackNodeRenderer : ITypeScriptNodeRenderer
{
    private readonly TanStackQueryAstRenderer _tanStackRenderer;

    /// <summary>
    /// Используется DI-контейнером для создания экземпляра.
    /// </summary>
    public TanStackNodeRenderer()
        : this(new TanStackQueryAstRenderer())
    {
    }

    /// <summary>
    /// Конструктор с внешним <see cref="TanStackQueryAstRenderer"/> (полезно для
    /// тестов и сценариев переиспользования состояния).
    /// </summary>
    public TanStackNodeRenderer(TanStackQueryAstRenderer tanStackRenderer)
    {
        _tanStackRenderer = tanStackRenderer ?? throw new ArgumentNullException(nameof(tanStackRenderer));
    }

    /// <inheritdoc/>
    public int Priority => 90;

    /// <inheritdoc/>
    public bool CanRender(AstNode node) => node
        is UseQueryNode
        or UseMutationNode
        or QueryKeyNode
        or QueryOptionsNode
        or MutationOptionsNode;

    /// <inheritdoc/>
    public string Render(AstNode node, ITypeScriptAstRenderer host)
    {
        return node switch
        {
            UseQueryNode useQuery => _tanStackRenderer.VisitUseQuery(useQuery),
            UseMutationNode useMutation => _tanStackRenderer.VisitUseMutation(useMutation),
            QueryKeyNode queryKey => _tanStackRenderer.VisitQueryKey(queryKey),
            QueryOptionsNode queryOptions => _tanStackRenderer.VisitQueryOptions(queryOptions),
            MutationOptionsNode mutationOptions => _tanStackRenderer.VisitMutationOptions(mutationOptions),
            _ => throw new NotSupportedException($"TanStackNodeRenderer can't render {node.GetType().Name}")
        };
    }
}
