using ValIO.Generators.TypeScript.Axios.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Axios.Ast.Visitors;

/// <summary>
/// Базовый класс для посетителя Axios AST узлов
/// </summary>
public abstract class AxiosAstVisitorBase<TResult> : TypeScriptAstVisitorBase<TResult>, IAxiosAstVisitor<TResult>
{
    // Абстрактные методы только для Axios узлов
    public abstract TResult VisitAxiosCall(AxiosCallExpressionNode node);
    public abstract TResult VisitAxiosConfig(AxiosConfigNode node);
}
