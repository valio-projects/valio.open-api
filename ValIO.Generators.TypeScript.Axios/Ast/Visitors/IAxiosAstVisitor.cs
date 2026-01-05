using ValIO.Generators.TypeScript.Axios.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Axios.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для Axios специфичных AST узлов
/// </summary>
public interface IAxiosAstVisitor<TResult>
{
    /// <summary>
    /// Посещает вызов Axios
    /// </summary>
    TResult VisitAxiosCall(AxiosCallExpressionNode node);
    
    /// <summary>
    /// Посещает конфигурацию Axios
    /// </summary>
    TResult VisitAxiosConfig(AxiosConfigNode node);
}
