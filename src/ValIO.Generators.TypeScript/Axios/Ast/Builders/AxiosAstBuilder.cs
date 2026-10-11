using System.Collections.Immutable;
using ValIO.Core.Ast.Builders;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Axios.Ast.Nodes;
using ValIO.Generators.TypeScript.Axios.Models;

namespace ValIO.Generators.TypeScript.Axios.Ast.Builders;

/// <summary>
/// Builds Axios-specific TypeScript AST nodes from transport-independent operation IR.
/// </summary>
public class AxiosAstBuilder
{
    private readonly HttpAstBuilder _httpBuilder;
    private readonly INamingConverter _namingConverter;
    private readonly Func<ValIO.Core.IR.Types.IrType, TypeNode> _typeBuilder;
    
    /// <summary>
    /// Конструктор
    /// </summary>
    /// <param name="namingConverter">Конвертер имен</param>
    /// <param name="typeBuilder">Функция для построения TypeNode из IrType</param>
    public AxiosAstBuilder(
        INamingConverter namingConverter,
        Func<ValIO.Core.IR.Types.IrType, TypeNode> typeBuilder)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeBuilder = typeBuilder ?? throw new ArgumentNullException(nameof(typeBuilder));
        _httpBuilder = new HttpAstBuilder();
    }
    
    /// <summary>
    /// Строит AxiosCallExpressionNode из IrOperation
    /// </summary>
    /// <param name="operation">IR операция</param>
    /// <param name="axiosInstance">Axios instance (опционально, по умолчанию "axios")</param>
    /// <returns>AST узел вызова Axios</returns>
    public AxiosCallExpressionNode BuildAxiosCall(
        IrOperation operation,
        ExpressionNode? axiosInstance = null)
    {
        var instance = axiosInstance ?? new IdentifierExpressionNode(new IdentifierNode("axios"));
        var config = BuildAxiosConfig(operation);
        
        return new AxiosCallExpressionNode(instance, config);
    }

    /// <summary>
    /// Строит AxiosCallExpressionNode с использованием params interface
    /// </summary>
    public AxiosCallExpressionNode BuildAxiosCallWithParams(
        IrOperation operation,
        AxiosConfigParamsSource paramsSource,
        ExpressionNode? axiosInstance = null)
    {
        var instance = axiosInstance ?? new IdentifierExpressionNode(new IdentifierNode("axios"));
        var config = BuildAxiosConfigWithParams(operation, paramsSource);
        return new AxiosCallExpressionNode(instance, config);
    }
    
    /// <summary>
    /// Builds an Axios request configuration from grouped or validated parameter sources.
    /// </summary>
    /// <param name="operation">The operation whose request is generated.</param>
    /// <param name="paramsSource">The expressions that provide route, query, body, and header values.</param>
    /// <returns>An Axios configuration AST node preserving OpenAPI wire names.</returns>
    public AxiosConfigNode BuildAxiosConfigWithParams(
        IrOperation operation,
        AxiosConfigParamsSource paramsSource)
    {
        var method = _httpBuilder.BuildHttpMethod(operation.HttpMethod);
        var url = BuildUrlWithSource(operation, paramsSource.RouteParamsSource);

        var queryParameters = operation.QueryParameters
            .Select(p => _httpBuilder.BuildQueryParameter(p, _typeBuilder))
            .ToImmutableList();

        var headers = operation.HeaderParameters
            .Select(p => _httpBuilder.BuildHeaderParameter(p, _typeBuilder))
            .ToImmutableList();

        var body = operation.RequestBody != null
            ? _httpBuilder.BuildRequestBody(operation.RequestBody, _typeBuilder)
            : null;

        var properties = new List<PropertyAssignmentNode>();

        properties.Add(new PropertyAssignmentNode(
            new IdentifierNode("method"),
            new LiteralExpressionNode(method.Method.ToString().ToUpperInvariant(), LiteralKind.String)));

        properties.Add(new PropertyAssignmentNode(
            new IdentifierNode("url"),
            url));

        var parameterModel = TypeScriptParameterModel.Create(operation);

        if (paramsSource.QueryParamsSource != null && operation.QueryParameters.Count > 0)
        {
            properties.Add(new PropertyAssignmentNode(
                new IdentifierNode("params"),
<<<<<<< HEAD
                TypeScriptWireParameterObject.BuildFromSource(
                    operation.QueryParameters,
                    paramsSource.QueryParamsSource,
                    parameterModel.QueryMayBeUndefined,
                    omitUndefinedValues: true)));
=======
                BuildQueryParamsSourceForAxios(operation, paramsSource.QueryParamsSource)));
>>>>>>> gitlab/fix/ts-nested-query-param-names
        }

        if (paramsSource.HeadersSource != null && operation.HeaderParameters.Count > 0)
        {
            // Axios expects Record<string, AxiosHeaderValue> or AxiosHeaders. The generated narrow header params
            // interface has no index signature, so cast the remapped wire object to the Axios header type.
            var headersAsAxiosType = new SimpleTypeNode(
                new IdentifierNode("AxiosRequestConfig[\"headers\"]"),
                isPrimitive: false);
            var wireHeaders = TypeScriptWireParameterObject.BuildFromSource(
                operation.HeaderParameters,
                paramsSource.HeadersSource,
                parameterModel.HeadersMayBeUndefined,
                omitUndefinedValues: true);
            var headersCast = new CastExpressionNode(
                headersAsAxiosType,
                wireHeaders,
                useAsOperator: true);
            properties.Add(new PropertyAssignmentNode(
                new IdentifierNode("headers"),
                headersCast));
        }

        if (paramsSource.BodySource != null && operation.RequestBody != null)
        {
            properties.Add(new PropertyAssignmentNode(
                new IdentifierNode("data"),
                paramsSource.BodySource));
        }

        if (operation.Responses.Any(IsBinarySuccessResponse))
        {
            properties.Add(new PropertyAssignmentNode(
                new IdentifierNode("responseType"),
                new LiteralExpressionNode("blob", LiteralKind.String)));
        }

        return new AxiosConfigNode(
            method: method,
            url: url,
            queryParameters: queryParameters,
            headers: headers,
            body: body,
            properties: properties.ToImmutableList());
    }

    private static bool IsBinarySuccessResponse(IrResponse response)
    {
        if (!int.TryParse(response.StatusCode, out var statusCode) || statusCode is < 200 or >= 300)
            return false;

        return Unwrap(response.Type) is ValIO.Core.IR.Types.IrPrimitiveType
        {
            PrimitiveKind: ValIO.Core.IR.Enums.IrPrimitiveKind.Binary or ValIO.Core.IR.Enums.IrPrimitiveKind.ByteArray
        };
    }

    private static ValIO.Core.IR.Types.IrType? Unwrap(ValIO.Core.IR.Types.IrType? type)
    {
        return type switch
        {
            ValIO.Core.IR.Types.IrNullableType nullable => Unwrap(nullable.InnerType),
            ValIO.Core.IR.Types.IrOptionalType optional => Unwrap(optional.InnerType),
            _ => type
        };
    }

    /// <summary>
<<<<<<< HEAD
    /// Builds an Axios request configuration for a legacy flat operation function.
=======
    /// Выражение для <c>params</c> в axios-config. Обычно это сам query-объект (<c>parsedQuery</c>) —
    /// его свойства и есть ключи query-string. Но если sanitize переименовал хотя бы один параметр
    /// (вложенная модель <c>Paging.Offset</c> → идентификатор <c>Paging_Offset</c>), объект передавать
    /// нельзя: axios отправит ключи с подчёркиванием, и ASP.NET model binding их не найдёт. В этом
    /// случае собираем литерал, отображающий «проводное» имя на sanitized-свойство. Отсутствующие
    /// опциональные параметры axios отбрасывает сам (<c>undefined</c> не сериализуется), поэтому
    /// условный спред не нужен.
    /// </summary>
    /// <param name="operation">IR-операция, чьи query-параметры проверяются на переименование.</param>
    /// <param name="queryParamsSource">Выражение исходного query-объекта.</param>
    /// <returns>Исходное выражение либо объектный литерал с «проводными» ключами.</returns>
    private static ExpressionNode BuildQueryParamsSourceForAxios(
        IrOperation operation,
        ExpressionNode queryParamsSource)
    {
        var needsWireNames = operation.QueryParameters
            .Any(p => !string.Equals(p.OriginalName, p.Name, StringComparison.Ordinal));

        if (!needsWireNames)
            return queryParamsSource;

        var mapped = operation.QueryParameters
            .Select(p => new PropertyAssignmentNode(
                new IdentifierNode(p.OriginalName),
                new MemberAccessExpressionNode(queryParamsSource, new IdentifierNode(p.Name))))
            .ToImmutableList();

        return new ObjectLiteralExpressionNode(mapped, preferMultilineFormat: true);
    }

    /// <summary>
    /// Строит AxiosConfigNode из IrOperation
>>>>>>> gitlab/fix/ts-nested-query-param-names
    /// </summary>
    /// <param name="operation">The operation whose request is generated.</param>
    /// <returns>An Axios configuration AST node preserving OpenAPI wire names.</returns>
    public AxiosConfigNode BuildAxiosConfig(IrOperation operation)
    {
        var method = _httpBuilder.BuildHttpMethod(operation.HttpMethod);
        var url = BuildUrl(operation);
        
        var queryParameters = operation.QueryParameters
            .Select(p => _httpBuilder.BuildQueryParameter(p, _typeBuilder))
            .ToImmutableList();
        
        var headers = operation.HeaderParameters
            .Select(p => _httpBuilder.BuildHeaderParameter(p, _typeBuilder))
            .ToImmutableList();
        
        var body = operation.RequestBody != null
            ? _httpBuilder.BuildRequestBody(operation.RequestBody, _typeBuilder)
            : null;
        
        // Build the properties consumed by AxiosConfigNode.
        var properties = new List<PropertyAssignmentNode>();
        
        // HTTP method.
        properties.Add(new PropertyAssignmentNode(
            new IdentifierNode("method"),
            new LiteralExpressionNode(method.Method.ToString().ToUpperInvariant(), LiteralKind.String)));
        
        // Request URL.
        properties.Add(new PropertyAssignmentNode(
            new IdentifierNode("url"),
            url));
        
        // Query parameters.
        if (queryParameters.Count > 0)
        {
            var paramsObject = TypeScriptWireParameterObject.BuildFromIdentifiers(
                operation.QueryParameters,
                omitUndefinedValues: true);
            properties.Add(new PropertyAssignmentNode(
                new IdentifierNode("params"),
                paramsObject));
        }
        
        // Request headers.
        if (headers.Count > 0)
        {
            var headersObject = TypeScriptWireParameterObject.BuildFromIdentifiers(
                operation.HeaderParameters,
                omitUndefinedValues: true);
            properties.Add(new PropertyAssignmentNode(
                new IdentifierNode("headers"),
                headersObject));
        }
        
        // Request body.
        if (body != null && operation.RequestBody != null)
        {
            var bodyParam = new IdentifierExpressionNode(new IdentifierNode("request"));
            properties.Add(new PropertyAssignmentNode(
                new IdentifierNode("data"),
                bodyParam));
        }
        
        return new AxiosConfigNode(
            method: method,
            url: url,
            queryParameters: queryParameters,
            headers: headers,
            body: body,
            properties: properties.ToImmutableList());
    }
    
    /// <summary>
    /// Строит URL выражение с использованием routeParamsSource
    /// </summary>
    /// <summary>
    /// Строит выражение URL (литерал или template) с подстановкой route-параметров.
    /// </summary>
    public ExpressionNode BuildUrlWithSource(IrOperation operation, ExpressionNode? routeParamsSource)
        // Path templating (placeholder → route access) is shared with every other transport; see
        // TypeScriptRequestPath.BuildUrl(operation, routeSource). Passing a rebound source (e.g. a validated
        // parsedRoute) is supported there, matching the Axios/fetch validation flow.
        => TypeScriptRequestPath.BuildUrl(operation, routeParamsSource);

    /// <summary>
    /// Builds a legacy flat URL expression, matching path placeholders by their original OpenAPI names and reading
    /// values from sanitized TypeScript identifiers.
    /// </summary>
    /// <param name="operation">The operation whose path is templated.</param>
    /// <returns>A string literal or template literal URL expression.</returns>
    private ExpressionNode BuildUrl(IrOperation operation)
    {
        var path = operation.Path;
        var pathParams = operation.PathParameters;
        
        if (pathParams.Count == 0)
        {
            // No path parameters require interpolation.
            return new LiteralExpressionNode(path, LiteralKind.String);
        }
        
        // Build a template literal with path-parameter substitutions.
        var parts = new List<TemplateLiteralPart>();
        var currentPath = path;
        
        foreach (var param in pathParams)
        {
            // OpenAPI path parameters use {wire-name}; TypeScript reads their sanitized identifier values.
            var paramPlaceholder = $"{{{param.OriginalName}}}";
            // Specifications sometimes vary placeholder casing, so match without regard to case.
            var index = currentPath.IndexOf(paramPlaceholder, StringComparison.OrdinalIgnoreCase);
            
            if (index >= 0)
            {
                // Preserve the literal path segment before the placeholder.
                if (index > 0)
                {
                    parts.Add(new TemplateLiteralPart(currentPath.Substring(0, index)));
                }
                
                // Insert the sanitized TypeScript value expression.
                parts.Add(new TemplateLiteralPart(
                    new IdentifierExpressionNode(new IdentifierNode(param.Name))));
                
                // Continue after the consumed wire placeholder.
                currentPath = currentPath.Substring(index + paramPlaceholder.Length);
            }
        }
        
        // Append the remaining literal path segment.
        if (currentPath.Length > 0)
        {
            parts.Add(new TemplateLiteralPart(currentPath));
        }
        
        // Fall back to the original literal when no declared placeholder was matched.
        if (parts.Count == 0)
        {
            return new LiteralExpressionNode(path, LiteralKind.String);
        }
        
        return new TemplateLiteralExpressionNode(parts.ToImmutableList());
    }
    
}
