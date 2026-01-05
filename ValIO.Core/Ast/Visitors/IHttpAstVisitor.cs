using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для HTTP специфичных AST узлов
/// </summary>
public interface IHttpAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    // HTTP специфичные узлы
    TResult VisitHttpMethod(HttpMethodNode node);
    TResult VisitRoute(RouteNode node);
    TResult VisitPathParameter(PathParameterNode node);
    TResult VisitQueryParameter(QueryParameterNode node);
    TResult VisitHeaderParameter(HeaderParameterNode node);
    TResult VisitRequestBody(RequestBodyNode node);
    TResult VisitResponseType(ResponseTypeNode node);
}
