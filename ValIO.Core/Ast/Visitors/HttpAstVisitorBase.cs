using ValIO.Core.Ast.Nodes.Http;

namespace ValIO.Core.Ast.Visitors;

/// <summary>
/// Базовый класс для посетителя HTTP AST узлов
/// </summary>
public abstract class HttpAstVisitorBase<TResult> : BaseAstVisitor<TResult>, IHttpAstVisitor<TResult>
{
    // Абстрактные методы только для HTTP узлов
    public abstract TResult VisitHttpMethod(HttpMethodNode node);
    public abstract TResult VisitRoute(RouteNode node);
    public abstract TResult VisitPathParameter(PathParameterNode node);
    public abstract TResult VisitQueryParameter(QueryParameterNode node);
    public abstract TResult VisitHeaderParameter(HeaderParameterNode node);
    public abstract TResult VisitRequestBody(RequestBodyNode node);
    public abstract TResult VisitResponseType(ResponseTypeNode node);
}
