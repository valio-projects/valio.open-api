using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Axios.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Axios.Ast.Renderers;

/// <summary>
/// Звено цепочки ответственности для Axios-узлов
/// (<see cref="AxiosCallExpressionNode"/>, <see cref="AxiosConfigNode"/>).
/// </summary>
/// <remarks>
/// Приоритет 90 — ниже валидаторов (Zod=100), чтобы Z*Node-узлы внутри Axios
/// config (params/headers/data) рендерились звеном валидатора, а не Axios.
/// </remarks>
public sealed class AxiosNodeRenderer : ITypeScriptNodeRenderer
{
    private readonly AxiosAstRenderer _axiosRenderer;

    /// <summary>
    /// Используется DI-контейнером для создания экземпляра.
    /// </summary>
    public AxiosNodeRenderer()
        : this(new AxiosAstRenderer())
    {
    }

    /// <summary>
    /// Конструктор с внешним <see cref="AxiosAstRenderer"/> (полезно для тестов).
    /// </summary>
    public AxiosNodeRenderer(AxiosAstRenderer axiosRenderer)
    {
        _axiosRenderer = axiosRenderer ?? throw new ArgumentNullException(nameof(axiosRenderer));
    }

    /// <inheritdoc/>
    public int Priority => 90;

    /// <inheritdoc/>
    public bool CanRender(AstNode node) => node is AxiosCallExpressionNode or AxiosConfigNode;

    /// <inheritdoc/>
    public string Render(AstNode node, ITypeScriptAstRenderer host)
    {
        return node switch
        {
            AxiosCallExpressionNode call => _axiosRenderer.VisitAxiosCall(call),
            AxiosConfigNode config => _axiosRenderer.VisitAxiosConfig(config),
            _ => throw new NotSupportedException($"AxiosNodeRenderer can't render {node.GetType().Name}")
        };
    }
}
