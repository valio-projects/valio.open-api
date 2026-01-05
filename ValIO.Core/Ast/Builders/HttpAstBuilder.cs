using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Ast.Builders;

/// <summary>
/// Построитель AST для HTTP специфичных узлов (language-agnostic)
/// </summary>
public class HttpAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly ITypeMapper _typeMapper;
    
    public HttpAstBuilder(INamingConverter namingConverter, ITypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
    }
    
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
    /// Строит RouteNode из пути и параметров
    /// </summary>
    public RouteNode BuildRoute(string path, IReadOnlyList<ParameterTemplateData> pathParameters, ITypeMapper typeMapper)
    {
        var pathParams = pathParameters.Select(p => BuildPathParameter(p, typeMapper)).ToImmutableList();
        return new RouteNode(path, pathParams);
    }
    
    /// <summary>
    /// Строит PathParameterNode
    /// </summary>
    public PathParameterNode BuildPathParameter(ParameterTemplateData param, ITypeMapper typeMapper)
    {
        var name = new IdentifierNode(param.VariableName);
        var type = BuildTypeFromTypeInfo(param.TypeInfo, typeMapper);
        return new PathParameterNode(name, type);
    }
    
    /// <summary>
    /// Строит QueryParameterNode
    /// </summary>
    public QueryParameterNode BuildQueryParameter(ParameterTemplateData param, ITypeMapper typeMapper)
    {
        var name = new IdentifierNode(param.VariableName);
        var type = BuildTypeFromTypeInfo(param.TypeInfo, typeMapper);
        return new QueryParameterNode(name, type, param.IsRequired);
    }
    
    /// <summary>
    /// Строит HeaderParameterNode
    /// </summary>
    public HeaderParameterNode BuildHeaderParameter(ParameterTemplateData param, ITypeMapper typeMapper)
    {
        var name = new IdentifierNode(param.VariableName);
        var type = BuildTypeFromTypeInfo(param.TypeInfo, typeMapper);
        return new HeaderParameterNode(name, type, param.IsRequired);
    }
    
    /// <summary>
    /// Строит RequestBodyNode
    /// </summary>
    public RequestBodyNode BuildRequestBody(TypeInfo? typeInfo, string? contentType, bool isRequired, ITypeMapper typeMapper)
    {
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));
        
        var type = BuildTypeFromTypeInfo(typeInfo, typeMapper);
        var kind = DetermineRequestBodyKind(contentType);
        
        return new RequestBodyNode(type, kind, contentType, isRequired);
    }
    
    /// <summary>
    /// Строит ResponseTypeNode
    /// </summary>
    public ResponseTypeNode BuildResponseType(TypeInfo typeInfo, int statusCode, ITypeMapper typeMapper)
    {
        var type = BuildTypeFromTypeInfo(typeInfo, typeMapper);
        var isSuccess = statusCode >= 200 && statusCode < 300;
        return new ResponseTypeNode(type, statusCode, isSuccess);
    }
    
    // Приватные вспомогательные методы
    
    private TypeNode BuildTypeFromTypeInfo(TypeInfo typeInfo, ITypeMapper typeMapper)
    {
        // TODO: Использовать TypeMapper для построения типа
        // Пока используем упрощенную версию
        if (typeInfo.IsCollection)
        {
            var elementType = typeInfo.GenericArguments.FirstOrDefault() ?? TypeInfo.Simple(typeInfo.BaseType);
            var elementTypeNode = BuildSimpleType(elementType.BaseType);
            return new ArrayTypeNode(elementTypeNode, isNullable: typeInfo.IsNullable, isOptional: typeInfo.IsOptional);
        }
        
        if (typeInfo.IsDictionary)
        {
            var keyType = BuildSimpleType("string");
            var valueType = typeInfo.GenericArguments.ElementAtOrDefault(1) != null
                ? BuildSimpleType(typeInfo.GenericArguments[1].BaseType)
                : BuildSimpleType("any");
            return new DictionaryTypeNode(keyType, valueType, isNullable: typeInfo.IsNullable, isOptional: typeInfo.IsOptional);
        }
        
        return BuildSimpleType(typeInfo.BaseType);
    }
    
    private SimpleTypeNode BuildSimpleType(string typeName)
    {
        return new SimpleTypeNode(new IdentifierNode(typeName), isPrimitive: false);
    }
    
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
