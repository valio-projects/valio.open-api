using System.Collections.Immutable;
using ValIO.Core.Ast.Builders;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Generators.TypeScript.Axios.Ast.Nodes;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.Axios.Ast.Builders;

/// <summary>
/// Построитель AST для Axios специфичных узлов
/// </summary>
public class AxiosAstBuilder
{
    private readonly HttpAstBuilder _httpBuilder;
    private readonly INamingConverter _namingConverter;
    private readonly ITypeMapper _typeMapper;
    
    public AxiosAstBuilder(INamingConverter namingConverter, ITypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _httpBuilder = new HttpAstBuilder(namingConverter, typeMapper);
    }
    
    /// <summary>
    /// Строит AxiosCallExpressionNode из OperationTemplateData
    /// </summary>
    public AxiosCallExpressionNode BuildAxiosCall(
        OperationTemplateData<TypeScriptOperationTemplateDataOptions> operation,
        ExpressionNode? axiosInstance = null)
    {
        var instance = axiosInstance ?? new IdentifierExpressionNode(new IdentifierNode("axios"));
        var config = BuildAxiosConfig(operation);
        
        return new AxiosCallExpressionNode(instance, config);
    }
    
    /// <summary>
    /// Строит AxiosConfigNode из OperationTemplateData
    /// </summary>
    public AxiosConfigNode BuildAxiosConfig(OperationTemplateData<TypeScriptOperationTemplateDataOptions> operation)
    {
        var method = _httpBuilder.BuildHttpMethod(operation.HttpMethod);
        var url = BuildUrl(operation);
        
        var queryParameters = operation.QueryParameters
            .Select(p => _httpBuilder.BuildQueryParameter(p, _typeMapper))
            .ToImmutableList();
        
        var headers = operation.HeaderParameters
            .Select(p => _httpBuilder.BuildHeaderParameter(p, _typeMapper))
            .ToImmutableList();
        
        var body = operation.HasRequestBody && operation.RequestBodyTypeInfo != null
            ? _httpBuilder.BuildRequestBody(operation.RequestBodyTypeInfo, operation.RequestBodyContentType, operation.IsRequestBodyRequired, _typeMapper)
            : null;
        
        // Строим properties для AxiosConfigNode
        var properties = new List<PropertyAssignmentNode>();
        
        // method
        properties.Add(new PropertyAssignmentNode(
            new IdentifierNode("method"),
            new LiteralExpressionNode(method.Method.ToString().ToUpperInvariant(), LiteralKind.String)));
        
        // url
        properties.Add(new PropertyAssignmentNode(
            new IdentifierNode("url"),
            url));
        
        // params (query parameters)
        if (queryParameters.Count > 0)
        {
            var paramsObject = BuildQueryParamsObject(queryParameters);
            properties.Add(new PropertyAssignmentNode(
                new IdentifierNode("params"),
                paramsObject));
        }
        
        // headers
        if (headers.Count > 0)
        {
            var headersObject = BuildHeadersObject(headers);
            properties.Add(new PropertyAssignmentNode(
                new IdentifierNode("headers"),
                headersObject));
        }
        
        // data (body)
        if (body != null && operation.HasRequestBody)
        {
            var bodyParam = new IdentifierExpressionNode(new IdentifierNode(operation.RequestBodyParameter ?? "data"));
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
    /// Строит URL выражение (может быть template literal)
    /// </summary>
    private ExpressionNode BuildUrl(OperationTemplateData<TypeScriptOperationTemplateDataOptions> operation)
    {
        var path = operation.Path;
        var pathParams = operation.PathParameters;
        
        if (pathParams.Count == 0)
        {
            // Простой строковый литерал
            return new LiteralExpressionNode(path, LiteralKind.String);
        }
        
        // Template literal с подстановкой параметров
        var parts = new List<TemplateLiteralPart>();
        var currentPath = path;
        
        foreach (var param in pathParams)
        {
            // Path parameters в OpenAPI имеют формат {paramName}, в TypeScript template literal - ${variableName}
            var paramPlaceholder = $"{{{param.Name}}}";
            var index = currentPath.IndexOf(paramPlaceholder, StringComparison.Ordinal);
            
            if (index >= 0)
            {
                // Добавляем текст до параметра
                if (index > 0)
                {
                    parts.Add(new TemplateLiteralPart(currentPath.Substring(0, index)));
                }
                
                // Добавляем выражение параметра (используем VariableName, а не Name)
                parts.Add(new TemplateLiteralPart(
                    new IdentifierExpressionNode(new IdentifierNode(param.VariableName))));
                
                // Обновляем путь
                currentPath = currentPath.Substring(index + paramPlaceholder.Length);
            }
        }
        
        // Добавляем оставшийся текст
        if (currentPath.Length > 0)
        {
            parts.Add(new TemplateLiteralPart(currentPath));
        }
        
        // Если не было найдено ни одного параметра, возвращаем простой литерал
        if (parts.Count == 0)
        {
            return new LiteralExpressionNode(path, LiteralKind.String);
        }
        
        return new TemplateLiteralExpressionNode(parts.ToImmutableList());
    }
    
    /// <summary>
    /// Строит объект для query parameters
    /// </summary>
    private ObjectLiteralExpressionNode BuildQueryParamsObject(IReadOnlyList<QueryParameterNode> queryParameters)
    {
        var properties = queryParameters.Select(qp =>
            new PropertyAssignmentNode(
                qp.Name,
                new IdentifierExpressionNode(qp.Name))).ToImmutableList();
        
        return new ObjectLiteralExpressionNode(properties);
    }
    
    /// <summary>
    /// Строит объект для headers
    /// </summary>
    private ObjectLiteralExpressionNode BuildHeadersObject(IReadOnlyList<HeaderParameterNode> headers)
    {
        var properties = headers.Select(h =>
            new PropertyAssignmentNode(
                h.Name,
                new IdentifierExpressionNode(h.Name))).ToImmutableList();
        
        return new ObjectLiteralExpressionNode(properties);
    }
}
