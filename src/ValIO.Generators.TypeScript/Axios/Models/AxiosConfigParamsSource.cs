using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Axios.Models;

/// <summary>
/// Источники выражений для построения Axios config при использовании params interface.
/// </summary>
/// <param name="RouteParamsSource">Выражение для доступа к route params (params.route или parsedRoute)</param>
/// <param name="QueryParamsSource">Выражение для axios config params (params.query или parsedQuery)</param>
/// <param name="BodySource">Выражение для axios config data (params.body или parsedBody)</param>
/// <param name="HeadersSource">Выражение для headers (params.headers)</param>
public sealed record AxiosConfigParamsSource(
    ExpressionNode? RouteParamsSource,
    ExpressionNode? QueryParamsSource,
    ExpressionNode? BodySource,
    ExpressionNode? HeadersSource);
