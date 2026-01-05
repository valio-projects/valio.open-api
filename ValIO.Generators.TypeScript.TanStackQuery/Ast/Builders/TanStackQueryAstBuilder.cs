using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;

/// <summary>
/// Построитель AST для TanStack Query специфичных узлов
/// </summary>
public class TanStackQueryAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly ITypeMapper _typeMapper;
    
    public TanStackQueryAstBuilder(INamingConverter namingConverter, ITypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
    }
    
    /// <summary>
    /// Строит QueryKeyNode из OperationTemplateData
    /// </summary>
    public QueryKeyNode BuildQueryKey(OperationTemplateData<TypeScriptOperationTemplateDataOptions> operation)
    {
        var parts = new List<ExpressionNode>();
        
        // Первая часть - имя операции или путь
        var operationName = new LiteralExpressionNode(operation.MethodName, LiteralKind.String);
        parts.Add(operationName);
        
        // Добавляем path параметры
        foreach (var param in operation.PathParameters)
        {
            parts.Add(new IdentifierExpressionNode(new IdentifierNode(param.VariableName)));
        }
        
        // Добавляем query параметры (опционально, если они влияют на ключ)
        foreach (var param in operation.QueryParameters.Where(p => p.IsRequired))
        {
            parts.Add(new IdentifierExpressionNode(new IdentifierNode(param.VariableName)));
        }
        
        return new QueryKeyNode(parts.ToImmutableList());
    }
    
    /// <summary>
    /// Строит UseQueryNode из OperationTemplateData
    /// </summary>
    public UseQueryNode BuildUseQuery(
        OperationTemplateData<TypeScriptOperationTemplateDataOptions> operation,
        ExpressionNode clientFunction)
    {
        var queryKey = BuildQueryKey(operation);
        var queryFn = clientFunction;
        var options = BuildQueryOptions(operation);
        
        return new UseQueryNode(queryKey, queryFn, options);
    }
    
    /// <summary>
    /// Строит UseMutationNode из OperationTemplateData
    /// </summary>
    public UseMutationNode BuildUseMutation(
        OperationTemplateData<TypeScriptOperationTemplateDataOptions> operation,
        ExpressionNode clientFunction)
    {
        var mutationFn = clientFunction;
        var options = BuildMutationOptions(operation);
        
        return new UseMutationNode(mutationFn, options);
    }
    
    /// <summary>
    /// Строит QueryOptionsNode из OperationTemplateData
    /// </summary>
    public QueryOptionsNode? BuildQueryOptions(OperationTemplateData<TypeScriptOperationTemplateDataOptions> operation)
    {
        // TODO: Добавить опции на основе OperationTemplateData
        // Пока возвращаем null (опции по умолчанию)
        return null;
    }
    
    /// <summary>
    /// Строит MutationOptionsNode из OperationTemplateData
    /// </summary>
    public MutationOptionsNode? BuildMutationOptions(OperationTemplateData<TypeScriptOperationTemplateDataOptions> operation)
    {
        // TODO: Добавить опции на основе OperationTemplateData
        // Пока возвращаем null (опции по умолчанию)
        return null;
    }
    
    /// <summary>
    /// Строит CallExpressionNode для вызова метода клиента
    /// </summary>
    public CallExpressionNode BuildClientFunctionCall(
        OperationTemplateData<TypeScriptOperationTemplateDataOptions> operation,
        IReadOnlyList<ParameterDeclarationNode> parameters)
    {
        var target = new IdentifierExpressionNode(new IdentifierNode(operation.MethodName));
        var arguments = parameters
            .Select(p => new IdentifierExpressionNode(p.Name) as ExpressionNode)
            .ToImmutableList();
        
        return new CallExpressionNode(target, arguments);
    }
}
