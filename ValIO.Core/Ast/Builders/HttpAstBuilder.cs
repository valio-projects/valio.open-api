using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;

namespace ValIO.Core.Ast.Builders;

/// <summary>
/// Построитель AST для HTTP специфичных узлов (language-agnostic, IR-based)
/// </summary>
public class HttpAstBuilder
{
    /// <summary>
    /// Строит HttpMethodNode из строки HTTP метода
    /// </summary>
    public HttpMethodNode BuildHttpMethod(string httpMethod)
    {
        var methodKind = httpMethod.ToUpperInvariant() switch
        {
            "GET" => HttpMethodKind.Get,
            "POST" => HttpMethodKind.Post,
            "PUT" => HttpMethodKind.Put,
            "DELETE" => HttpMethodKind.Delete,
            "PATCH" => HttpMethodKind.Patch,
            "HEAD" => HttpMethodKind.Head,
            "OPTIONS" => HttpMethodKind.Options,
            _ => throw new ArgumentException($"Unsupported HTTP method: {httpMethod}", nameof(httpMethod))
        };
        
        return new HttpMethodNode(methodKind);
    }
    
    /// <summary>
    /// Строит RouteNode из пути и path параметров
    /// </summary>
    public RouteNode BuildRoute(string path, IReadOnlyList<IrParameter> pathParameters, Func<IrType, TypeNode> buildType)
    {
        var pathParams = pathParameters.Select(p => BuildPathParameter(p, buildType)).ToImmutableList();
        return new RouteNode(path, pathParams);
    }
    
    /// <summary>
    /// Строит PathParameterNode из IrParameter
    /// </summary>
    public PathParameterNode BuildPathParameter(IrParameter param, Func<IrType, TypeNode> buildType)
    {
        var name = new IdentifierNode(param.Name);
        var type = buildType(param.Type);
        return new PathParameterNode(name, type);
    }
    
    /// <summary>
    /// Строит QueryParameterNode из IrParameter
    /// </summary>
    public QueryParameterNode BuildQueryParameter(IrParameter param, Func<IrType, TypeNode> buildType)
    {
        var name = new IdentifierNode(param.Name);
        var type = buildType(param.Type);
        return new QueryParameterNode(name, type, param.IsRequired);
    }
    
    /// <summary>
    /// Строит HeaderParameterNode из IrParameter
    /// </summary>
    public HeaderParameterNode BuildHeaderParameter(IrParameter param, Func<IrType, TypeNode> buildType)
    {
        var name = new IdentifierNode(param.Name);
        var type = buildType(param.Type);
        return new HeaderParameterNode(name, type, param.IsRequired);
    }
    
    /// <summary>
    /// Строит RequestBodyNode из IrRequestBody
    /// </summary>
    public RequestBodyNode BuildRequestBody(IrRequestBody requestBody, Func<IrType, TypeNode> buildType)
    {
        var type = buildType(requestBody.Type);
        var kind = DetermineRequestBodyKind(requestBody.ContentType);
        
        return new RequestBodyNode(type, kind, requestBody.ContentType, requestBody.IsRequired);
    }
    
    /// <summary>
    /// Строит ResponseTypeNode из IrResponse
    /// </summary>
    public ResponseTypeNode BuildResponseType(IrResponse response, Func<IrType, TypeNode> buildType)
    {
        if (response.Type == null)
            throw new ArgumentException("Response must have a type", nameof(response));
        
        var type = buildType(response.Type);
        var statusCode = int.TryParse(response.StatusCode, out var code) ? code : 200;
        var isSuccess = statusCode >= 200 && statusCode < 300;
        
        return new ResponseTypeNode(type, statusCode, isSuccess);
    }
    
    // Приватные вспомогательные методы
    
    private static RequestBodyKind DetermineRequestBodyKind(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return RequestBodyKind.Json;
        
        return contentType.ToLowerInvariant() switch
        {
            var ct when ct.Contains("application/json") => RequestBodyKind.Json,
            var ct when ct.Contains("multipart/form-data") => RequestBodyKind.MultipartFormData,
            var ct when ct.Contains("application/x-www-form-urlencoded") => RequestBodyKind.FormData,
            _ => RequestBodyKind.Raw
        };
    }
}
