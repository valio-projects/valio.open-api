using ValIO.Generators.TypeScript.Axios.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Axios.Ast.Renderers;

/// <summary>
/// Интерфейс рендерера AST для Axios
/// </summary>
public interface IAxiosAstRenderer
{
    /// <summary>
    /// Рендерит AxiosCallExpressionNode в TypeScript код
    /// </summary>
    string Render(AxiosCallExpressionNode node);
}
