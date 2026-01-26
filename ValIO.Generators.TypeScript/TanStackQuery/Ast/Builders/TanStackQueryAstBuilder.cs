using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;

/// <summary>
/// Построитель AST для TanStack Query специфичных узлов
/// Использует IR для построения AST узлов
/// </summary>
public class TanStackQueryAstBuilder
{
    private readonly INamingConverter _namingConverter;
    
    /// <summary>
    /// Конструктор
    /// </summary>
    /// <param name="namingConverter">Конвертер имен</param>
    public TanStackQueryAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }
    
    /// <summary>
    /// Строит QueryKeyNode из IrOperation
    /// </summary>
    /// <param name="operation">IR операция</param>
    /// <returns>AST узел query key</returns>
    public QueryKeyNode BuildQueryKey(IrOperation operation)
    {
        var parts = new List<ExpressionNode>();
        
        // Первая часть - путь операции
        var pathLiteral = new LiteralExpressionNode(operation.Path, LiteralKind.String);
        parts.Add(pathLiteral);
        
        // Добавляем path параметры
        foreach (var param in operation.PathParameters)
        {
            parts.Add(new IdentifierExpressionNode(new IdentifierNode(param.Name)));
        }
        
        // Добавляем query параметры (все, так как они влияют на ключ)
        foreach (var param in operation.QueryParameters)
        {
            parts.Add(new IdentifierExpressionNode(new IdentifierNode(param.Name)));
        }
        
        return new QueryKeyNode(parts.ToImmutableList());
    }
    
    /// <summary>
    /// Строит UseQueryNode из IrOperation
    /// </summary>
    /// <param name="operation">IR операция</param>
    /// <param name="clientFunction">Выражение вызова функции клиента</param>
    /// <returns>AST узел useQuery хука</returns>
    public UseQueryNode BuildUseQuery(
        IrOperation operation,
        ExpressionNode clientFunction)
    {
        var queryKey = BuildQueryKey(operation);
        var queryFn = clientFunction;
        var options = BuildQueryOptions(operation);
        
        return new UseQueryNode(queryKey, queryFn, options);
    }
    
    /// <summary>
    /// Строит UseMutationNode из IrOperation
    /// </summary>
    /// <param name="operation">IR операция</param>
    /// <param name="clientFunction">Выражение вызова функции клиента</param>
    /// <returns>AST узел useMutation хука</returns>
    public UseMutationNode BuildUseMutation(
        IrOperation operation,
        ExpressionNode clientFunction)
    {
        var mutationFn = clientFunction;
        var options = BuildMutationOptions(operation);
        
        return new UseMutationNode(mutationFn, options);
    }
    
    /// <summary>
    /// Строит QueryOptionsNode из IrOperation
    /// </summary>
    /// <param name="operation">IR операция</param>
    /// <returns>AST узел опций query или null</returns>
    public QueryOptionsNode? BuildQueryOptions(IrOperation operation)
    {
        // TODO: Добавить опции на основе IrOperation
        // Пока возвращаем null (опции по умолчанию)
        return null;
    }
    
    /// <summary>
    /// Строит MutationOptionsNode из IrOperation
    /// </summary>
    /// <param name="operation">IR операция</param>
    /// <returns>AST узел опций mutation или null</returns>
    public MutationOptionsNode? BuildMutationOptions(IrOperation operation)
    {
        // TODO: Добавить опции на основе IrOperation
        // Пока возвращаем null (опции по умолчанию)
        return null;
    }
    
    /// <summary>
    /// Строит CallExpressionNode для вызова метода клиента из IrOperation
    /// </summary>
    /// <param name="operation">IR операция</param>
    /// <param name="parameters">Параметры функции</param>
    /// <returns>AST узел вызова функции клиента</returns>
    public CallExpressionNode BuildClientFunctionCall(
        IrOperation operation,
        IReadOnlyList<ParameterDeclarationNode> parameters)
    {
        var functionName = _namingConverter.ToCamelCase(operation.MethodName);
        var target = new IdentifierExpressionNode(new IdentifierNode(functionName));
        var arguments = parameters
            .Select(p => new IdentifierExpressionNode(p.Name) as ExpressionNode)
            .ToImmutableList();
        
        return new CallExpressionNode(target, arguments);
    }
}
