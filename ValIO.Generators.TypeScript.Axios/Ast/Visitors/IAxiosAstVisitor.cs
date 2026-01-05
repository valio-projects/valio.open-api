using ValIO.Generators.TypeScript.Axios.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Axios.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для Axios специфичных AST узлов
/// </summary>
public interface IAxiosAstVisitor<TResult> : ITypeScriptAstVisitor<TResult>
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
