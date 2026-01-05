using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Вспомогательные методы для построения выражений TypeScript
/// </summary>
public static class ExpressionBuilders
{
    /// <summary>
    /// Строит вызов fetch API
    /// </summary>
    public static CallExpressionNode BuildFetchCall(
        ExpressionNode url,
        ExpressionNode? options = null)
    {
        var fetchIdentifier = new IdentifierExpressionNode(new IdentifierNode("fetch"));
        var arguments = options != null
            ? new[] { url, options }.ToImmutableList<ExpressionNode>()
            : new[] { url }.ToImmutableList<ExpressionNode>();
        
        return new CallExpressionNode(fetchIdentifier, arguments);
    }
    
    /// <summary>
    /// Строит вызов метода объекта (например, response.json())
    /// </summary>
    public static CallExpressionNode BuildMethodCall(
        ExpressionNode target,
        string methodName,
        IReadOnlyList<ExpressionNode>? arguments = null)
    {
        var memberAccess = new MemberAccessExpressionNode(
            target,
            new IdentifierNode(methodName));
        
        return new CallExpressionNode(memberAccess, arguments?.ToImmutableList());
    }
    
    // Await обрабатывается на уровне рендеринга через метаданные или специальный узел
    // Для async функций await будет добавляться автоматически при рендеринге
    
    /// <summary>
    /// Строит объявление переменной с const
    /// </summary>
    public static VariableDeclarationNode BuildConstVariable(
        string name,
        ExpressionNode? initializer = null,
        TypeNode? type = null)
    {
        return new VariableDeclarationNode(
            name: new IdentifierNode(name),
            declarationKind: VariableDeclarationKind.Const,
            type: type,
            initializer: initializer);
    }
    
    /// <summary>
    /// Строит объявление переменной с let
    /// </summary>
    public static VariableDeclarationNode BuildLetVariable(
        string name,
        ExpressionNode? initializer = null,
        TypeNode? type = null)
    {
        return new VariableDeclarationNode(
            name: new IdentifierNode(name),
            declarationKind: VariableDeclarationKind.Let,
            type: type,
            initializer: initializer);
    }
    
    /// <summary>
    /// Строит объект Record<string, string> для headers
    /// </summary>
    public static ObjectLiteralExpressionNode BuildHeadersObject(
        IReadOnlyList<(string Name, ExpressionNode Value)> headers)
    {
        var properties = headers.Select(h =>
            new PropertyAssignmentNode(
                name: new IdentifierNode(h.Name),
                value: h.Value)).ToImmutableList();
        
        return new ObjectLiteralExpressionNode(properties);
    }
    
    /// <summary>
    /// Строит объект для query parameters
    /// </summary>
    public static ObjectLiteralExpressionNode BuildQueryParamsObject(
        IReadOnlyList<(string Name, ExpressionNode Value)> queryParams)
    {
        var properties = queryParams.Select(qp =>
            new PropertyAssignmentNode(
                name: new IdentifierNode(qp.Name),
                value: qp.Value)).ToImmutableList();
        
        return new ObjectLiteralExpressionNode(properties);
    }
    
    /// <summary>
    /// Строит объект для fetch options
    /// </summary>
    public static ObjectLiteralExpressionNode BuildFetchOptions(
        string method,
        ExpressionNode? headers = null,
        ExpressionNode? body = null)
    {
        var properties = new List<PropertyAssignmentNode>();
        
        // method
        properties.Add(new PropertyAssignmentNode(
            name: new IdentifierNode("method"),
            value: new LiteralExpressionNode(method, LiteralKind.String)));
        
        // headers
        if (headers != null)
        {
            properties.Add(new PropertyAssignmentNode(
                name: new IdentifierNode("headers"),
                value: headers));
        }
        
        // body
        if (body != null)
        {
            properties.Add(new PropertyAssignmentNode(
                name: new IdentifierNode("body"),
                value: body));
        }
        
        return new ObjectLiteralExpressionNode(properties.ToImmutableList());
    }
    
    /// <summary>
    /// Строит вызов JSON.stringify
    /// </summary>
    public static CallExpressionNode BuildJsonStringify(ExpressionNode value)
    {
        var jsonIdentifier = new IdentifierExpressionNode(new IdentifierNode("JSON"));
        var stringifyMember = new MemberAccessExpressionNode(
            jsonIdentifier,
            new IdentifierNode("stringify"));
        
        return new CallExpressionNode(stringifyMember, new[] { value }.ToImmutableList());
    }
    
    /// <summary>
    /// Строит проверку на undefined
    /// </summary>
    public static BinaryExpressionNode BuildUndefinedCheck(
        ExpressionNode left,
        BinaryOperator @operator = BinaryOperator.NotEqual)
    {
        var undefinedLiteral = new LiteralExpressionNode("undefined", LiteralKind.String);
        return new BinaryExpressionNode(left, @operator, undefinedLiteral);
    }
}
