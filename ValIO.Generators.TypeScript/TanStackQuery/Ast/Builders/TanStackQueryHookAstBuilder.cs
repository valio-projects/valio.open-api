using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;

/// <summary>
/// Строит AST (SourceFileNode) для TanStack Query hook-файла.
/// Использует AST-узлы для типов, тел методов и объявлений.
/// </summary>
public sealed class TanStackQueryHookAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptParamsAstBuilder _paramsBuilder;
    private readonly TypeScriptResultsAstBuilder _resultsBuilder;
    private readonly TanStackQueryTypeBuilder _typeBuilder;

    public TanStackQueryHookAstBuilder(
        INamingConverter namingConverter,
        Func<ValIO.Core.IR.Types.IrType, ValIO.Core.Ast.Nodes.TypeNode> typeBuilder)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _paramsBuilder = new TypeScriptParamsAstBuilder(namingConverter, typeBuilder);
        _resultsBuilder = new TypeScriptResultsAstBuilder(namingConverter, typeBuilder);
        _typeBuilder = new TanStackQueryTypeBuilder(namingConverter);
    }

    /// <summary>
    /// Строит SourceFileNode для hook-файла операции.
    /// </summary>
    public SourceFileNode BuildHookSourceFile(
        IrOperation operation,
        string operationFileName,
        bool isMutation)
    {
        var hookBaseName = _namingConverter.ToPascalCase(operation.MethodName);
        var hasParams = _paramsBuilder.HasParameters(operation);
        var paramsTypeName = hasParams ? _paramsBuilder.ParamsInterfaceName(operation.MethodName) : null;

        var resultTypeName = _resultsBuilder.HasResponsesWithBody(operation)
            ? _resultsBuilder.ResultTypeName(operation.MethodName)
            : null;

        var dataType = _typeBuilder.BuildDataType(resultTypeName);
        var variablesType = _typeBuilder.BuildVariablesType(paramsTypeName);

        var imports = BuildImports(operation, operationFileName, hasParams, paramsTypeName, resultTypeName, isMutation);
        var declarations = isMutation
            ? BuildMutationDeclarations(operation, hookBaseName, paramsTypeName, resultTypeName, dataType, variablesType)
            : BuildQueryDeclarations(operation, hookBaseName, paramsTypeName, hasParams, resultTypeName, dataType);

        return new SourceFileNode(imports: imports, declarations: declarations);
    }

    private static IReadOnlyList<ImportNode> BuildImports(
        IrOperation operation,
        string operationFileName,
        bool hasParams,
        string? paramsTypeName,
        string? resultTypeName,
        bool isMutation)
    {
        var tanstackItems = isMutation
            ? new[]
            {
                new IdentifierNode("useMutation"),
                new IdentifierNode("useIsMutating"),
                new IdentifierNode("QueryClient"),
                new IdentifierNode("UseMutationOptions"),
                new IdentifierNode("UseMutationResult"),
                new IdentifierNode("MutationFunction"),
            }
            : new[]
            {
                new IdentifierNode("useInfiniteQuery"),
                new IdentifierNode("useQuery"),
                new IdentifierNode("useIsFetching"),
                new IdentifierNode("QueryClient"),
                new IdentifierNode("QueryFunction"),
                new IdentifierNode("QueryFunctionContext"),
                new IdentifierNode("UseQueryOptions"),
                new IdentifierNode("UseInfiniteQueryOptions"),
                new IdentifierNode("UseQueryResult"),
                new IdentifierNode("UseInfiniteQueryResult"),
                new IdentifierNode("InfiniteData"),
            };

        var items = new List<ImportNode>
        {
            new(items: tanstackItems.ToImmutableList(), from: "@tanstack/react-query", kind: ImportKind.Named),
            new(items: new[] { new IdentifierNode(operation.MethodName) }.ToImmutableList(), from: $"./{operationFileName}", kind: ImportKind.Named)
        };

        if (hasParams && paramsTypeName != null)
            items.Add(new ImportNode(items: new[] { new IdentifierNode(paramsTypeName) }.ToImmutableList(), from: $"./{operationFileName}.params", kind: ImportKind.Named, isTypeOnly: true));

        if (resultTypeName != null)
            items.Add(new ImportNode(items: new[] { new IdentifierNode(resultTypeName) }.ToImmutableList(), from: $"./{operationFileName}.results", kind: ImportKind.Named, isTypeOnly: true));

        items.Add(new ImportNode(items: new[] { new IdentifierNode("AxiosRequestConfig") }.ToImmutableList(), from: "axios", kind: ImportKind.Named, isTypeOnly: true));

        return items;
    }

    private IReadOnlyList<DeclarationNode> BuildQueryDeclarations(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams,
        string? resultTypeName,
        TypeNode dataType)
    {
        var pathParts = GetQueryKeyPathParts(operation);
        var decls = new List<DeclarationNode>();

        decls.Add(_typeBuilder.BuildQueryOptionsParamsInterface(hookBaseName, dataType, isInfinite: false));
        decls.Add(_typeBuilder.BuildQueryOptionsParamsInterface(hookBaseName, dataType, isInfinite: true));
        decls.Add(_typeBuilder.BuildQueryOptionsResultTypeAlias(hookBaseName, dataType, isInfinite: false));
        decls.Add(_typeBuilder.BuildQueryOptionsResultTypeAlias(hookBaseName, dataType, isInfinite: true));

        var paramsType = paramsTypeName != null ? TanStackQueryTypeBuilder.SimpleType(paramsTypeName) : null;
        var queryOptionsParamsType = new IdentifierNode($"Get{hookBaseName}QueryOptionsParams");
        var infiniteQueryOptionsParamsType = new IdentifierNode($"Get{hookBaseName}InfiniteQueryOptionsParams");
        var queryOptionsResultType = new IdentifierNode($"Get{hookBaseName}QueryOptionsResult");
        var infiniteQueryOptionsResultType = new IdentifierNode($"Get{hookBaseName}InfiniteQueryOptionsResult");

        decls.Add(BuildGetQueryKeyFn($"get{hookBaseName}InfiniteQueryKey", pathParts, hasParams, paramsType, includeInfinite: true));
        decls.Add(BuildGetQueryKeyFn($"get{hookBaseName}QueryKey", pathParts, hasParams, paramsType, includeInfinite: false));
        decls.Add(BuildGetInfiniteQueryOptionsFn(operation, hookBaseName, paramsType, hasParams, dataType, new IdentifierNode($"get{hookBaseName}InfiniteQueryKey"), new IdentifierNode($"get{hookBaseName}InfiniteQueryOptions"), infiniteQueryOptionsParamsType, infiniteQueryOptionsResultType));
        decls.Add(BuildGetQueryOptionsFn(operation, hookBaseName, paramsType, hasParams, dataType, new IdentifierNode($"get{hookBaseName}QueryKey"), new IdentifierNode($"get{hookBaseName}QueryOptions"), queryOptionsParamsType, queryOptionsResultType));
        decls.Add(new TypeAliasDeclarationNode(new IdentifierNode($"{hookBaseName}QueryResult"), dataType));
        decls.Add(new TypeAliasDeclarationNode(new IdentifierNode($"{hookBaseName}QueryError"), TanStackQueryTypeBuilder.SimpleType("Error")));
        decls.Add(BuildUseQueryFn(operation, hookBaseName, paramsType, hasParams, queryOptionsParamsType, queryOptionsResultType, dataType));
        decls.Add(BuildUseInfiniteQueryFn(operation, hookBaseName, paramsType, hasParams, infiniteQueryOptionsParamsType, infiniteQueryOptionsResultType, dataType));
        decls.Add(BuildPrefetchQueryFn(operation, hookBaseName, paramsType, hasParams, queryOptionsParamsType, queryOptionsResultType));
        decls.Add(BuildPrefetchInfiniteQueryFn(operation, hookBaseName, paramsType, hasParams, infiniteQueryOptionsParamsType, infiniteQueryOptionsResultType));
        decls.Add(BuildUseIsFetchingFn(hookBaseName, paramsType, hasParams));

        return decls;
    }

    private IReadOnlyList<DeclarationNode> BuildMutationDeclarations(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        string? resultTypeName,
        TypeNode dataType,
        TypeNode variablesType)
    {
        var decls = new List<DeclarationNode>();

        decls.Add(_typeBuilder.BuildMutationOptionsParamsInterface(hookBaseName, dataType, variablesType));
        decls.Add(_typeBuilder.BuildMutationOptionsResultInterface(hookBaseName, dataType, variablesType));

        var optionsParamsType = new IdentifierNode($"Get{hookBaseName}MutationOptionsParams");
        var optionsResultType = new IdentifierNode($"Get{hookBaseName}MutationOptionsResult");

        decls.Add(BuildGetMutationOptionsFn(operation, hookBaseName, variablesType, optionsParamsType, optionsResultType));
        decls.Add(new TypeAliasDeclarationNode(new IdentifierNode($"{hookBaseName}MutationResult"), dataType));
        decls.Add(new TypeAliasDeclarationNode(new IdentifierNode($"{hookBaseName}MutationError"), TanStackQueryTypeBuilder.SimpleType("Error")));
        decls.Add(BuildUseMutationFn(operation, hookBaseName, optionsParamsType, optionsResultType, dataType, variablesType));
        decls.Add(BuildUseIsMutatingFn(operation, hookBaseName));

        return decls;
    }

    private static IReadOnlyList<string> GetQueryKeyPathParts(IrOperation operation)
    {
        var path = operation.Path.Trim('/');
        return string.IsNullOrEmpty(path) ? new[] { "api" } : path.Split('/').Where(p => !string.IsNullOrEmpty(p)).ToList();
    }

    private FunctionDeclarationNode BuildGetQueryKeyFn(
        string name,
        IReadOnlyList<string> pathParts,
        bool hasParams,
        TypeNode? paramsType,
        bool includeInfinite)
    {
        var elements = new List<ExpressionNode>();
        if (includeInfinite)
            elements.Add(new LiteralExpressionNode("infinite", LiteralKind.String));
        foreach (var part in pathParts)
            elements.Add(new LiteralExpressionNode(part, LiteralKind.String));
        if (hasParams)
        {
            var cond = new ConditionalExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("params")),
                new ArrayLiteralExpressionNode(new[] { new IdentifierExpressionNode(new IdentifierNode("params")) }.ToImmutableList()),
                new ArrayLiteralExpressionNode(ImmutableList<ExpressionNode>.Empty));
            elements.Add(new SpreadExpressionNode(cond));
        }

        var parameters = hasParams && paramsType != null
            ? new[] { new ParameterDeclarationNode(new IdentifierNode("params"), paramsType, isRequired: false) }.ToImmutableList()
            : ImmutableList<ParameterDeclarationNode>.Empty;

        return new FunctionDeclarationNode(
            new IdentifierNode(name),
            returnType: _typeBuilder.BuildQueryKeyReturnType(),
            parameters: parameters,
            body: new BlockNode(new[] { new ReturnStatementNode(new ArrayLiteralExpressionNode(elements.ToImmutableList(), preferMultilineFormat: true)) }.ToImmutableList()),
            isArrowFunction: true,
            isAsync: false,
            documentation: new CommentNode(includeInfinite ? "Query key for useInfiniteQuery." : "Query key for useQuery.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildGetInfiniteQueryOptionsFn(
        IrOperation operation,
        string hookBaseName,
        TypeNode? paramsType,
        bool hasParams,
        TypeNode dataType,
        IdentifierNode getQueryKeyFnName,
        IdentifierNode getQueryOptionsFnName,
        IdentifierNode optionsParamsType,
        IdentifierNode optionsResultType)
    {
        return BuildGetQueryOptionsFnInternal(operation, hookBaseName, paramsType, hasParams, dataType, getQueryKeyFnName, getQueryOptionsFnName, optionsParamsType, optionsResultType, isInfinite: true);
    }

    private FunctionDeclarationNode BuildGetQueryOptionsFn(
        IrOperation operation,
        string hookBaseName,
        TypeNode? paramsType,
        bool hasParams,
        TypeNode dataType,
        IdentifierNode getQueryKeyFnName,
        IdentifierNode getQueryOptionsFnName,
        IdentifierNode optionsParamsType,
        IdentifierNode optionsResultType)
    {
        return BuildGetQueryOptionsFnInternal(operation, hookBaseName, paramsType, hasParams, dataType, getQueryKeyFnName, getQueryOptionsFnName, optionsParamsType, optionsResultType, isInfinite: false);
    }

    private FunctionDeclarationNode BuildGetQueryOptionsFnInternal(
        IrOperation operation,
        string hookBaseName,
        TypeNode? paramsType,
        bool hasParams,
        TypeNode dataType,
        IdentifierNode getQueryKeyFnName,
        IdentifierNode getQueryOptionsFnName,
        IdentifierNode optionsParamsType,
        IdentifierNode optionsResultType,
        bool isInfinite)
    {
        var tQueryKey = TanStackQueryTypeBuilder.TQueryKeyTypeParameter;
        var queryKeyConstraint = _typeBuilder.BuildQueryKeyReturnType();
        var genericParams = new[]
        {
            new GenericParameterNode(new IdentifierNode("TQueryKey"), queryKeyConstraint, queryKeyConstraint)
        }.ToImmutableList();

        var optionsRef = new IdentifierExpressionNode(new IdentifierNode("options"));
        var queryOptsRef = new MemberAccessExpressionNode(optionsRef, new IdentifierNode("query"), useOptionalChaining: true);
        var queryKeyFromOpts = new MemberAccessExpressionNode(queryOptsRef, new IdentifierNode("queryKey"), useOptionalChaining: true);

        var queryKeyVar = new VariableDeclarationStatementNode(
            new IdentifierNode("queryKey"),
            _typeBuilder.BuildQueryKeyReturnType(),
            new BinaryExpressionNode(
                queryKeyFromOpts,
                BinaryOperator.NullishCoalescing,
                new CallExpressionNode(
                    new IdentifierExpressionNode(getQueryKeyFnName),
                    hasParams ? new[] { new IdentifierExpressionNode(new IdentifierNode("params")) }.ToImmutableList() : ImmutableList<ExpressionNode>.Empty)));

        var apiCallArgs = new List<ExpressionNode>();
        if (hasParams)
            apiCallArgs.Add(new IdentifierExpressionNode(new IdentifierNode("params")));
        apiCallArgs.Add(new MemberAccessExpressionNode(optionsRef, new IdentifierNode("request"), useOptionalChaining: true));
        apiCallArgs.Add(new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("context")), new IdentifierNode("signal")));

        var queryFnExpr = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode(operation.MethodName)),
            apiCallArgs.ToImmutableList());

        var queryFnArrow = new ArrowFunctionExpressionNode(
            new[] { new ParameterDeclarationNode(new IdentifierNode("context"), _typeBuilder.BuildQueryFunctionContextType(tQueryKey), isRequired: true) }.ToImmutableList(),
            queryFnExpr);

        var returnObjProps = new List<PropertyAssignmentNode>
        {
            new(new IdentifierNode("queryKey"), new IdentifierExpressionNode(new IdentifierNode("queryKey"))),
            new(new IdentifierNode("queryFn"), queryFnArrow),
        };
        if (isInfinite)
        {
            returnObjProps.Add(new PropertyAssignmentNode(new IdentifierNode("initialPageParam"), new LiteralExpressionNode(0, LiteralKind.Number)));
            returnObjProps.Add(new PropertyAssignmentNode(new IdentifierNode("getNextPageParam"), new ArrowFunctionExpressionNode(
                ImmutableList<ParameterDeclarationNode>.Empty,
                new LiteralExpressionNode(null, LiteralKind.Null))));
        }
        var returnObj = new ObjectLiteralExpressionNode(returnObjProps.ToImmutableList(), type: null, metadata: null, preferMultilineFormat: true);

        var resultType = isInfinite
            ? _typeBuilder.BuildUseInfiniteQueryOptionsWithQueryKey(dataType, tQueryKey)
            : _typeBuilder.BuildUseQueryOptionsWithQueryKey(dataType, tQueryKey);
        var statements = new List<StatementNode>
        {
            queryKeyVar,
            new VariableDeclarationStatementNode(new IdentifierNode("result"), resultType, returnObj),
            new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("result")))
        };

        var parameters = new List<ParameterDeclarationNode>();
        if (hasParams && paramsType != null)
            parameters.Add(new ParameterDeclarationNode(new IdentifierNode("params"), paramsType, isRequired: true));
        parameters.Add(new ParameterDeclarationNode(new IdentifierNode("options"), new SimpleTypeNode(optionsParamsType, isPrimitive: false), isRequired: false));

        return new FunctionDeclarationNode(
            getQueryOptionsFnName,
            returnType: resultType,
            parameters: parameters.ToImmutableList(),
            body: new BlockNode(statements.ToImmutableList()),
            isArrowFunction: true,
            documentation: new CommentNode(isInfinite ? "Options for useInfiniteQuery." : "Options for useQuery.", CommentKind.JSDoc),
            isExported: true,
            genericParameters: genericParams);
    }

    private FunctionDeclarationNode BuildUseQueryFn(
        IrOperation operation,
        string hookBaseName,
        TypeNode? paramsType,
        bool hasParams,
        IdentifierNode optionsParamsType,
        IdentifierNode optionsResultType,
        TypeNode dataType)
    {
        var getOptsArgs = hasParams
            ? new[] { new IdentifierExpressionNode(new IdentifierNode("params")), new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList()
            : new[] { new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList();

        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("queryOptions"),
                new SimpleTypeNode(optionsResultType, isPrimitive: false),
                new CallExpressionNode(new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}QueryOptions")), getOptsArgs)),
            new ReturnStatementNode(new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("useQuery")),
                new[] { new IdentifierExpressionNode(new IdentifierNode("queryOptions")), new IdentifierExpressionNode(new IdentifierNode("queryClient")) }.ToImmutableList()))
        }.ToImmutableList());

        var parameters = BuildUseQueryParams(paramsType, hasParams, optionsParamsType);
        return new FunctionDeclarationNode(
            new IdentifierNode($"use{hookBaseName}"),
            returnType: _typeBuilder.BuildUseQueryResultType(dataType),
            parameters: parameters,
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Fetches and caches data.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildUseInfiniteQueryFn(
        IrOperation operation,
        string hookBaseName,
        TypeNode? paramsType,
        bool hasParams,
        IdentifierNode optionsParamsType,
        IdentifierNode optionsResultType,
        TypeNode dataType)
    {
        var getOptsArgs = hasParams
            ? new[] { new IdentifierExpressionNode(new IdentifierNode("params")), new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList()
            : new[] { new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList();

        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("queryOptions"),
                new SimpleTypeNode(optionsResultType, isPrimitive: false),
                new CallExpressionNode(new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}InfiniteQueryOptions")), getOptsArgs)),
            new ReturnStatementNode(new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("useInfiniteQuery")),
                new[] { new IdentifierExpressionNode(new IdentifierNode("queryOptions")), new IdentifierExpressionNode(new IdentifierNode("queryClient")) }.ToImmutableList()))
        }.ToImmutableList());

        return new FunctionDeclarationNode(
            new IdentifierNode($"use{hookBaseName}Infinite"),
            returnType: _typeBuilder.BuildUseInfiniteQueryResultType(dataType),
            parameters: BuildUseQueryParams(paramsType, hasParams, optionsParamsType),
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Fetches paginated data.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildPrefetchQueryFn(
        IrOperation operation,
        string hookBaseName,
        TypeNode? paramsType,
        bool hasParams,
        IdentifierNode optionsParamsType,
        IdentifierNode optionsResultType)
    {
        var getOptsArgs = hasParams
            ? new[] { new IdentifierExpressionNode(new IdentifierNode("params")), new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList()
            : new[] { new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList();

        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("queryOptions"),
                new SimpleTypeNode(optionsResultType, isPrimitive: false),
                new CallExpressionNode(new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}QueryOptions")), getOptsArgs)),
            new ExpressionStatementNode(new AwaitExpressionNode(new CallExpressionNode(
                new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("queryClient")), new IdentifierNode("prefetchQuery")),
                new[] { new IdentifierExpressionNode(new IdentifierNode("queryOptions")) }.ToImmutableList()))),
            new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("queryClient")))
        }.ToImmutableList());

        var parameters = new List<ParameterDeclarationNode>
        {
            new(new IdentifierNode("queryClient"), TanStackQueryTypeBuilder.SimpleType("QueryClient"), isRequired: true)
        };
        if (hasParams && paramsType != null)
            parameters.Add(new ParameterDeclarationNode(new IdentifierNode("params"), paramsType, isRequired: true));
        parameters.Add(new ParameterDeclarationNode(new IdentifierNode("options"), new SimpleTypeNode(optionsParamsType, isPrimitive: false), isRequired: false));

        return new FunctionDeclarationNode(
            new IdentifierNode($"prefetch{hookBaseName}Query"),
            returnType: _typeBuilder.BuildPromiseQueryClientType(),
            parameters: parameters.ToImmutableList(),
            body: body,
            isArrowFunction: true,
            isAsync: true,
            documentation: new CommentNode("Prefetches data into cache.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildPrefetchInfiniteQueryFn(
        IrOperation operation,
        string hookBaseName,
        TypeNode? paramsType,
        bool hasParams,
        IdentifierNode optionsParamsType,
        IdentifierNode optionsResultType)
    {
        var getOptsArgs = hasParams
            ? new[] { new IdentifierExpressionNode(new IdentifierNode("params")), new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList()
            : new[] { new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList();

        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("queryOptions"),
                new SimpleTypeNode(optionsResultType, isPrimitive: false),
                new CallExpressionNode(new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}InfiniteQueryOptions")), getOptsArgs)),
            new ExpressionStatementNode(new AwaitExpressionNode(new CallExpressionNode(
                new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("queryClient")), new IdentifierNode("prefetchInfiniteQuery")),
                new[] { new IdentifierExpressionNode(new IdentifierNode("queryOptions")) }.ToImmutableList()))),
            new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("queryClient")))
        }.ToImmutableList());

        var parameters = new List<ParameterDeclarationNode>
        {
            new(new IdentifierNode("queryClient"), TanStackQueryTypeBuilder.SimpleType("QueryClient"), isRequired: true)
        };
        if (hasParams && paramsType != null)
            parameters.Add(new ParameterDeclarationNode(new IdentifierNode("params"), paramsType, isRequired: true));
        parameters.Add(new ParameterDeclarationNode(new IdentifierNode("options"), new SimpleTypeNode(optionsParamsType, isPrimitive: false), isRequired: false));

        return new FunctionDeclarationNode(
            new IdentifierNode($"prefetch{hookBaseName}InfiniteQuery"),
            returnType: _typeBuilder.BuildPromiseQueryClientType(),
            parameters: parameters.ToImmutableList(),
            body: body,
            isArrowFunction: true,
            isAsync: true,
            documentation: new CommentNode("Prefetches infinite query data.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildUseIsFetchingFn(string hookBaseName, TypeNode? paramsType, bool hasParams)
    {
        var getKeyCall = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}QueryKey")),
            hasParams ? new[] { new IdentifierExpressionNode(new IdentifierNode("params")) }.ToImmutableList() : ImmutableList<ExpressionNode>.Empty);

        var body = new BlockNode(new[]
        {
            new ReturnStatementNode(new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("useIsFetching")),
                new[] { new ObjectLiteralExpressionNode(new[] { new PropertyAssignmentNode(new IdentifierNode("queryKey"), getKeyCall) }.ToImmutableList()) }.ToImmutableList()))
        }.ToImmutableList());

        var parameters = hasParams && paramsType != null
            ? new[] { new ParameterDeclarationNode(new IdentifierNode("params"), paramsType, isRequired: false) }.ToImmutableList()
            : ImmutableList<ParameterDeclarationNode>.Empty;

        return new FunctionDeclarationNode(
            new IdentifierNode($"useIsFetching{hookBaseName}"),
            returnType: TanStackQueryTypeBuilder.SimpleType("number", isPrimitive: true),
            parameters: parameters,
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Returns true if this query is currently fetching.", CommentKind.JSDoc),
            isExported: true);
    }

    private static IReadOnlyList<ParameterDeclarationNode> BuildUseQueryParams(
        TypeNode? paramsType,
        bool hasParams,
        IdentifierNode optionsParamsType)
    {
        var list = new List<ParameterDeclarationNode>();
        if (hasParams && paramsType != null)
            list.Add(new ParameterDeclarationNode(new IdentifierNode("params"), paramsType, isRequired: true));
        list.Add(new ParameterDeclarationNode(new IdentifierNode("options"), new SimpleTypeNode(optionsParamsType, isPrimitive: false), isRequired: false));
        list.Add(new ParameterDeclarationNode(new IdentifierNode("queryClient"), TanStackQueryTypeBuilder.SimpleType("QueryClient"), isRequired: false));
        return list;
    }

    private FunctionDeclarationNode BuildGetMutationOptionsFn(
        IrOperation operation,
        string hookBaseName,
        TypeNode variablesType,
        IdentifierNode optionsParamsType,
        IdentifierNode optionsResultType)
    {
        var mutationKey = new ArrayLiteralExpressionNode(
            new[] { new LiteralExpressionNode(_namingConverter.ToCamelCase(operation.MethodName), LiteralKind.String) }.ToImmutableList());

        var mutationFn = new ArrowFunctionExpressionNode(
            new[] { new ParameterDeclarationNode(new IdentifierNode("variables"), variablesType, isRequired: true) }.ToImmutableList(),
            new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode(operation.MethodName)),
                new[] { new IdentifierExpressionNode(new IdentifierNode("variables")), new IdentifierExpressionNode(new IdentifierNode("requestOptions")) }.ToImmutableList()));

        var resultObj = new ObjectLiteralExpressionNode(new PropertyAssignmentNode[]
        {
            new(new IdentifierNode("mutationKey"), mutationKey),
            new(new IdentifierNode("mutationFn"), mutationFn),
        }.ToImmutableList(), type: null, metadata: null, preferMultilineFormat: true);

        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("requestOptions"),
                _typeBuilder.BuildRequestOptionsType(),
                new MemberAccessExpressionNode(
                    new BinaryExpressionNode(
                        new IdentifierExpressionNode(new IdentifierNode("options")),
                        BinaryOperator.NullishCoalescing,
                        new ObjectLiteralExpressionNode(ImmutableList<PropertyAssignmentNode>.Empty)),
                    new IdentifierNode("request"))),
            new VariableDeclarationStatementNode(
                new IdentifierNode("result"),
                new SimpleTypeNode(optionsResultType, isPrimitive: false),
                resultObj),
            new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("result")))
        }.ToImmutableList());

        return new FunctionDeclarationNode(
            new IdentifierNode($"get{hookBaseName}MutationOptions"),
            returnType: new SimpleTypeNode(optionsResultType, isPrimitive: false),
            parameters: new[] { new ParameterDeclarationNode(new IdentifierNode("options"), new SimpleTypeNode(optionsParamsType, isPrimitive: false), isRequired: false) }.ToImmutableList(),
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Options for useMutation.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildUseMutationFn(
        IrOperation operation,
        string hookBaseName,
        IdentifierNode optionsParamsType,
        IdentifierNode optionsResultType,
        TypeNode dataType,
        TypeNode variablesType)
    {
        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("mutationOptions"),
                new SimpleTypeNode(optionsResultType, isPrimitive: false),
                new CallExpressionNode(new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}MutationOptions")), new[] { new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList())),
            new ReturnStatementNode(new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("useMutation")),
                new[] { new IdentifierExpressionNode(new IdentifierNode("mutationOptions")), new IdentifierExpressionNode(new IdentifierNode("queryClient")) }.ToImmutableList()))
        }.ToImmutableList());

        return new FunctionDeclarationNode(
            new IdentifierNode($"use{hookBaseName}"),
            returnType: _typeBuilder.BuildUseMutationResultType(dataType, variablesType),
            parameters: new[]
            {
                new ParameterDeclarationNode(new IdentifierNode("options"), new SimpleTypeNode(optionsParamsType, isPrimitive: false), isRequired: false),
                new ParameterDeclarationNode(new IdentifierNode("queryClient"), TanStackQueryTypeBuilder.SimpleType("QueryClient"), isRequired: false)
            }.ToImmutableList(),
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Mutation hook.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildUseIsMutatingFn(IrOperation operation, string hookBaseName)
    {
        var mutationKey = new ArrayLiteralExpressionNode(
            new[] { new LiteralExpressionNode(_namingConverter.ToCamelCase(operation.MethodName), LiteralKind.String) }.ToImmutableList());

        var body = new BlockNode(new[]
        {
            new ReturnStatementNode(new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("useIsMutating")),
                new[] { new ObjectLiteralExpressionNode(new[] { new PropertyAssignmentNode(new IdentifierNode("mutationKey"), mutationKey) }.ToImmutableList()) }.ToImmutableList()))
        }.ToImmutableList());

        return new FunctionDeclarationNode(
            new IdentifierNode($"useIsMutating{hookBaseName}"),
            returnType: TanStackQueryTypeBuilder.SimpleType("number", isPrimitive: true),
            parameters: ImmutableList<ParameterDeclarationNode>.Empty,
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Returns true if this mutation is in progress.", CommentKind.JSDoc),
            isExported: true);
    }
}
