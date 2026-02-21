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
/// Использует TypeScript AST nodes: ImportNode, FunctionDeclarationNode, TypeAliasDeclarationNode.
/// </summary>
public sealed class TanStackQueryHookAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptParamsAstBuilder _paramsBuilder;

    public TanStackQueryHookAstBuilder(
        INamingConverter namingConverter,
        Func<ValIO.Core.IR.Types.IrType, ValIO.Core.Ast.Nodes.TypeNode> typeBuilder)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _paramsBuilder = new TypeScriptParamsAstBuilder(namingConverter, typeBuilder);
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

        var imports = BuildImports(operation, operationFileName, hasParams, paramsTypeName);
        var declarations = isMutation
            ? BuildMutationDeclarations(operation, hookBaseName, paramsTypeName)
            : BuildQueryDeclarations(operation, hookBaseName, paramsTypeName, hasParams);

        return new SourceFileNode(imports: imports, declarations: declarations);
    }

    private static IReadOnlyList<ImportNode> BuildImports(
        IrOperation operation,
        string operationFileName,
        bool hasParams,
        string? paramsTypeName)
    {
        var items = new List<ImportNode>
        {
            new(
                items: new[]
                {
                    new IdentifierNode("useInfiniteQuery"),
                    new IdentifierNode("useMutation"),
                    new IdentifierNode("useQuery"),
                    new IdentifierNode("useIsFetching"),
                    new IdentifierNode("useIsMutating"),
                    new IdentifierNode("QueryClient"),
                    new IdentifierNode("QueryFunction"),
                    new IdentifierNode("UseQueryOptions"),
                    new IdentifierNode("UseInfiniteQueryOptions"),
                    new IdentifierNode("UseMutationOptions"),
                    new IdentifierNode("UseQueryResult"),
                    new IdentifierNode("UseInfiniteQueryResult"),
                    new IdentifierNode("UseMutationResult"),
                    new IdentifierNode("InfiniteData"),
                    new IdentifierNode("MutationFunction"),
                }.ToImmutableList(),
                from: "@tanstack/react-query",
                kind: ImportKind.Named),
            new(
                items: new[] { new IdentifierNode(operation.MethodName) }.ToImmutableList(),
                from: $"./{operationFileName}",
                kind: ImportKind.Named)
        };

        if (hasParams && paramsTypeName != null)
        {
            items.Add(new ImportNode(
                items: new[] { new IdentifierNode(paramsTypeName) }.ToImmutableList(),
                from: $"./{operationFileName}.params",
                kind: ImportKind.Named,
                isTypeOnly: true));
        }

        items.Add(new ImportNode(
            items: new[] { new IdentifierNode("AxiosRequestConfig") }.ToImmutableList(),
            from: "axios",
            kind: ImportKind.Named,
            isTypeOnly: true));

        return items;
    }

    private IReadOnlyList<DeclarationNode> BuildQueryDeclarations(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        var pathParts = GetQueryKeyPathParts(operation);
        var decls = new List<DeclarationNode>();

        decls.Add(BuildGetQueryKeyFn($"get{hookBaseName}InfiniteQueryKey", pathParts, hasParams, paramsTypeName, includeInfinite: true));
        decls.Add(BuildGetQueryKeyFn($"get{hookBaseName}QueryKey", pathParts, hasParams, paramsTypeName, includeInfinite: false));
        decls.Add(BuildGetInfiniteQueryOptionsFn(operation, hookBaseName, paramsTypeName, hasParams));
        decls.Add(BuildGetQueryOptionsFn(operation, hookBaseName, paramsTypeName, hasParams));
        decls.Add(BuildTypeAlias($"{hookBaseName}QueryResult", $"NonNullable<Awaited<ReturnType<typeof {operation.MethodName}>>>"));
        decls.Add(BuildTypeAlias($"{hookBaseName}QueryError", "Error"));
        decls.Add(BuildUseQueryFn(operation, hookBaseName, paramsTypeName, hasParams));
        decls.Add(BuildUseInfiniteQueryFn(operation, hookBaseName, paramsTypeName, hasParams));
        decls.Add(BuildPrefetchQueryFn(operation, hookBaseName, paramsTypeName, hasParams));
        decls.Add(BuildPrefetchInfiniteQueryFn(operation, hookBaseName, paramsTypeName, hasParams));
        decls.Add(BuildUseIsFetchingFn(hookBaseName, paramsTypeName, hasParams));

        return decls;
    }

    private IReadOnlyList<DeclarationNode> BuildMutationDeclarations(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName)
    {
        var variablesType = paramsTypeName ?? "void";
        var decls = new List<DeclarationNode>();

        decls.Add(BuildGetMutationOptionsFn(operation, hookBaseName, variablesType));
        decls.Add(BuildTypeAlias($"{hookBaseName}MutationResult", $"NonNullable<Awaited<ReturnType<typeof {operation.MethodName}>>>"));
        decls.Add(BuildTypeAlias($"{hookBaseName}MutationError", "Error"));
        decls.Add(BuildUseMutationFn(operation, hookBaseName, variablesType));
        decls.Add(BuildUseIsMutatingFn(operation, hookBaseName));

        return decls;
    }

    private static IReadOnlyList<string> GetQueryKeyPathParts(IrOperation operation)
    {
        var path = operation.Path.Trim('/');
        return string.IsNullOrEmpty(path)
            ? new[] { "api" }
            : path.Split('/').Where(p => !string.IsNullOrEmpty(p)).ToList();
    }

    private FunctionDeclarationNode BuildGetQueryKeyFn(
        string name,
        IReadOnlyList<string> pathParts,
        bool hasParams,
        string? paramsTypeName,
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

        var parameters = hasParams && paramsTypeName != null
            ? new[]
            {
                new ParameterDeclarationNode(
                    new IdentifierNode("params"),
                    new SimpleTypeNode(new IdentifierNode(paramsTypeName), isPrimitive: false),
                    isRequired: false)
            }.ToImmutableList()
            : ImmutableList<ParameterDeclarationNode>.Empty;

        return new FunctionDeclarationNode(
            new IdentifierNode(name),
            returnType: null,
            parameters: parameters,
            body: new BlockNode(new[] { new ReturnStatementNode(new ArrayLiteralExpressionNode(elements.ToImmutableList(), preferMultilineFormat: true)) }.ToImmutableList()),
            isArrowFunction: true,
            isAsync: false,
            documentation: new CommentNode(includeInfinite
                ? "Query key for useInfiniteQuery. Includes 'infinite' prefix for pagination."
                : "Query key for useQuery. Used as cache identifier.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildGetInfiniteQueryOptionsFn(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        return BuildGetQueryOptionsFnInternal(operation, $"get{hookBaseName}InfiniteQueryKey", $"get{hookBaseName}InfiniteQueryOptions", paramsTypeName, hasParams);
    }

    private FunctionDeclarationNode BuildGetQueryOptionsFn(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        return BuildGetQueryOptionsFnInternal(operation, $"get{hookBaseName}QueryKey", $"get{hookBaseName}QueryOptions", paramsTypeName, hasParams);
    }

    private FunctionDeclarationNode BuildGetQueryOptionsFnInternal(
        IrOperation operation,
        string getQueryKeyFnName,
        string getQueryOptionsFnName,
        string? paramsTypeName,
        bool hasParams)
    {
        var optionsRef = new IdentifierExpressionNode(new IdentifierNode("options"));
        var queryOptsRef = new MemberAccessExpressionNode(optionsRef, new IdentifierNode("query"), useOptionalChaining: true);
        var queryKeyFromOpts = new MemberAccessExpressionNode(queryOptsRef, new IdentifierNode("queryKey"), useOptionalChaining: true);

        var queryKeyVar = new VariableDeclarationStatementNode(
            new IdentifierNode("queryKey"),
            null,
            new BinaryExpressionNode(
                queryKeyFromOpts,
                BinaryOperator.NullishCoalescing,
                new CallExpressionNode(
                    new IdentifierExpressionNode(new IdentifierNode(getQueryKeyFnName)),
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
            new[] { new ParameterDeclarationNode(new IdentifierNode("context"), new SimpleTypeNode(new IdentifierNode("object"), isPrimitive: false), isRequired: true) }.ToImmutableList(),
            queryFnExpr);

        var returnObjProps = new List<PropertyAssignmentNode>
        {
            new(new IdentifierNode("queryKey"), new IdentifierExpressionNode(new IdentifierNode("queryKey"))),
            new(new IdentifierNode("queryFn"), queryFnArrow),
        };
        if (getQueryOptionsFnName.Contains("Infinite"))
        {
            returnObjProps.Add(new PropertyAssignmentNode(new IdentifierNode("initialPageParam"), new LiteralExpressionNode(0, LiteralKind.Number)));
            returnObjProps.Add(new PropertyAssignmentNode(new IdentifierNode("getNextPageParam"), new ArrowFunctionExpressionNode(
                ImmutableList<ParameterDeclarationNode>.Empty,
                new LiteralExpressionNode(null, LiteralKind.Null))));
        }
        var returnObj = new ObjectLiteralExpressionNode(returnObjProps.ToImmutableList());

        var statements = new List<StatementNode>
        {
            queryKeyVar,
            new ReturnStatementNode(returnObj)
        };

        var parameters = new List<ParameterDeclarationNode>();
        if (hasParams && paramsTypeName != null)
        {
            parameters.Add(new ParameterDeclarationNode(
                new IdentifierNode("params"),
                new SimpleTypeNode(new IdentifierNode(paramsTypeName), isPrimitive: false),
                isRequired: true));
        }
        var optionsTypeStr = getQueryOptionsFnName.Contains("Infinite")
            ? $"{{ query?: Partial<UseInfiniteQueryOptions<InfiniteData<NonNullable<Awaited<ReturnType<typeof {operation.MethodName}>>>>, Error>>; request?: AxiosRequestConfig }}"
            : $"{{ query?: Partial<UseQueryOptions<NonNullable<Awaited<ReturnType<typeof {operation.MethodName}>>>, Error>>; request?: AxiosRequestConfig }}";
        parameters.Add(new ParameterDeclarationNode(
            new IdentifierNode("options"),
            new RawTypeNode(optionsTypeStr),
            isRequired: false));

        return new FunctionDeclarationNode(
            new IdentifierNode(getQueryOptionsFnName),
            returnType: null,
            parameters: parameters.ToImmutableList(),
            body: new BlockNode(statements.ToImmutableList()),
            isArrowFunction: true,
            documentation: new CommentNode(getQueryOptionsFnName.Contains("Infinite")
                ? "Options for useInfiniteQuery. Adds pagination (initialPageParam, getNextPageParam)."
                : "Options for useQuery. Contains queryKey and queryFn.",
                CommentKind.JSDoc),
            isExported: true);
    }

    private static TypeAliasDeclarationNode BuildTypeAlias(string name, string typeName)
    {
        return new TypeAliasDeclarationNode(
            new IdentifierNode(name),
            new SimpleTypeNode(new IdentifierNode(typeName), isPrimitive: false));
    }

    private FunctionDeclarationNode BuildUseQueryFn(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        var getOptsArgs = hasParams
            ? new[] { new IdentifierExpressionNode(new IdentifierNode("params")), new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList()
            : new[] { new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList();

        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("queryOptions"),
                null,
                new CallExpressionNode(new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}QueryOptions")), getOptsArgs)),
            new ReturnStatementNode(new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("useQuery")),
                new[] { new IdentifierExpressionNode(new IdentifierNode("queryOptions")), new IdentifierExpressionNode(new IdentifierNode("queryClient")) }.ToImmutableList()))
        }.ToImmutableList());

        var parameters = BuildUseQueryParams(paramsTypeName, hasParams, $"get{hookBaseName}QueryOptions");
        return new FunctionDeclarationNode(
            new IdentifierNode($"use{hookBaseName}"),
            returnType: null,
            parameters: parameters,
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Fetches and caches data. Use for one-time or refetch-on-mount.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildUseInfiniteQueryFn(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        var getOptsArgs = hasParams
            ? new[] { new IdentifierExpressionNode(new IdentifierNode("params")), new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList()
            : new[] { new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList();

        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("queryOptions"),
                null,
                new CallExpressionNode(new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}InfiniteQueryOptions")), getOptsArgs)),
            new ReturnStatementNode(new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("useInfiniteQuery")),
                new[] { new IdentifierExpressionNode(new IdentifierNode("queryOptions")), new IdentifierExpressionNode(new IdentifierNode("queryClient")) }.ToImmutableList()))
        }.ToImmutableList());

        return new FunctionDeclarationNode(
            new IdentifierNode($"use{hookBaseName}Infinite"),
            returnType: null,
            parameters: BuildUseQueryParams(paramsTypeName, hasParams, $"get{hookBaseName}InfiniteQueryOptions"),
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Fetches paginated data. Use for infinite scroll / load more.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildPrefetchQueryFn(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        var getOptsArgs = hasParams
            ? new[] { new IdentifierExpressionNode(new IdentifierNode("params")), new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList()
            : new[] { new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList();

        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("queryOptions"),
                null,
                new CallExpressionNode(new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}QueryOptions")), getOptsArgs)),
            new ExpressionStatementNode(new AwaitExpressionNode(new CallExpressionNode(
                new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("queryClient")), new IdentifierNode("prefetchQuery")),
                new[] { new IdentifierExpressionNode(new IdentifierNode("queryOptions")) }.ToImmutableList()))),
            new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("queryClient")))
        }.ToImmutableList());

        var parameters = new List<ParameterDeclarationNode>
        {
            new(new IdentifierNode("queryClient"), new SimpleTypeNode(new IdentifierNode("QueryClient"), isPrimitive: false), isRequired: true)
        };
        if (hasParams && paramsTypeName != null)
            parameters.Add(new ParameterDeclarationNode(new IdentifierNode("params"), new SimpleTypeNode(new IdentifierNode(paramsTypeName), isPrimitive: false), isRequired: true));
        parameters.Add(new ParameterDeclarationNode(
            new IdentifierNode("options"),
            new RawTypeNode($"Parameters<typeof get{hookBaseName}QueryOptions>[1]"),
            isRequired: false,
            documentation: new CommentNode("query: UseQueryOptions, request: AxiosRequestConfig", CommentKind.JSDoc)));

        return new FunctionDeclarationNode(
            new IdentifierNode($"prefetch{hookBaseName}Query"),
            returnType: null,
            parameters: parameters.ToImmutableList(),
            body: body,
            isArrowFunction: true,
            isAsync: true,
            documentation: new CommentNode("Prefetches data into cache. Call before navigation for instant display.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildPrefetchInfiniteQueryFn(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        var getOptsArgs = hasParams
            ? new[] { new IdentifierExpressionNode(new IdentifierNode("params")), new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList()
            : new[] { new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList();

        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("queryOptions"),
                null,
                new CallExpressionNode(new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}InfiniteQueryOptions")), getOptsArgs)),
            new ExpressionStatementNode(new AwaitExpressionNode(new CallExpressionNode(
                new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("queryClient")), new IdentifierNode("prefetchInfiniteQuery")),
                new[] { new IdentifierExpressionNode(new IdentifierNode("queryOptions")) }.ToImmutableList()))),
            new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("queryClient")))
        }.ToImmutableList());

        var parameters = new List<ParameterDeclarationNode>
        {
            new(new IdentifierNode("queryClient"), new SimpleTypeNode(new IdentifierNode("QueryClient"), isPrimitive: false), isRequired: true)
        };
        if (hasParams && paramsTypeName != null)
            parameters.Add(new ParameterDeclarationNode(new IdentifierNode("params"), new SimpleTypeNode(new IdentifierNode(paramsTypeName), isPrimitive: false), isRequired: true));
        parameters.Add(new ParameterDeclarationNode(
            new IdentifierNode("options"),
            new RawTypeNode($"Parameters<typeof get{hookBaseName}InfiniteQueryOptions>[1]"),
            isRequired: false,
            documentation: new CommentNode("query: UseInfiniteQueryOptions, request: AxiosRequestConfig", CommentKind.JSDoc)));

        return new FunctionDeclarationNode(
            new IdentifierNode($"prefetch{hookBaseName}InfiniteQuery"),
            returnType: null,
            parameters: parameters.ToImmutableList(),
            body: body,
            isArrowFunction: true,
            isAsync: true,
            documentation: new CommentNode("Prefetches infinite query data. Use for preloading next pages.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildUseIsFetchingFn(string hookBaseName, string? paramsTypeName, bool hasParams)
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

        var parameters = hasParams && paramsTypeName != null
            ? new[] { new ParameterDeclarationNode(new IdentifierNode("params"), new SimpleTypeNode(new IdentifierNode(paramsTypeName), isPrimitive: false), isRequired: false) }.ToImmutableList()
            : ImmutableList<ParameterDeclarationNode>.Empty;

        return new FunctionDeclarationNode(
            new IdentifierNode($"useIsFetching{hookBaseName}"),
            returnType: null,
            parameters: parameters,
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Returns true if this query is currently fetching.", CommentKind.JSDoc),
            isExported: true);
    }

    private static IReadOnlyList<ParameterDeclarationNode> BuildUseQueryParams(string? paramsTypeName, bool hasParams, string getOptionsFnName)
    {
        var list = new List<ParameterDeclarationNode>();
        if (hasParams && paramsTypeName != null)
            list.Add(new ParameterDeclarationNode(new IdentifierNode("params"), new SimpleTypeNode(new IdentifierNode(paramsTypeName), isPrimitive: false), isRequired: true));
        list.Add(new ParameterDeclarationNode(
            new IdentifierNode("options"),
            new RawTypeNode($"Parameters<typeof {getOptionsFnName}>[1]"),
            isRequired: false,
            documentation: new CommentNode("query: UseQueryOptions, request: AxiosRequestConfig", CommentKind.JSDoc)));
        list.Add(new ParameterDeclarationNode(
            new IdentifierNode("queryClient"),
            new SimpleTypeNode(new IdentifierNode("QueryClient"), isPrimitive: false),
            isRequired: false,
            documentation: new CommentNode("Uses default QueryClient if not provided", CommentKind.JSDoc)));
        return list;
    }

    private FunctionDeclarationNode BuildGetMutationOptionsFn(
        IrOperation operation,
        string hookBaseName,
        string variablesType)
    {
        var mutationKey = new ArrayLiteralExpressionNode(
            new[] { new LiteralExpressionNode(_namingConverter.ToCamelCase(operation.MethodName), LiteralKind.String) }.ToImmutableList());

        var mutationFnBody = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode(operation.MethodName)),
            new[] { new IdentifierExpressionNode(new IdentifierNode("variables")), new IdentifierExpressionNode(new IdentifierNode("requestOptions")) }.ToImmutableList());

        var mutationFn = new ArrowFunctionExpressionNode(
            new[] { new ParameterDeclarationNode(new IdentifierNode("variables"), new SimpleTypeNode(new IdentifierNode(variablesType), isPrimitive: false), isRequired: true) }.ToImmutableList(),
            mutationFnBody);

        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("requestOptions"),
                null,
                new MemberAccessExpressionNode(
                    new BinaryExpressionNode(
                        new IdentifierExpressionNode(new IdentifierNode("options")),
                        BinaryOperator.NullishCoalescing,
                        new ObjectLiteralExpressionNode(ImmutableList<PropertyAssignmentNode>.Empty)),
                    new IdentifierNode("request"))),
            new ReturnStatementNode(new ObjectLiteralExpressionNode(new PropertyAssignmentNode[]
            {
                new PropertyAssignmentNode(new IdentifierNode("mutationKey"), mutationKey),
                new PropertyAssignmentNode(new IdentifierNode("mutationFn"), mutationFn),
            }.ToImmutableList()))
        }.ToImmutableList());

        var mutationOptionsType = $"{{ mutation?: UseMutationOptions<NonNullable<Awaited<ReturnType<typeof {operation.MethodName}>>>, Error, {variablesType}>; request?: AxiosRequestConfig }}";
        return new FunctionDeclarationNode(
            new IdentifierNode($"get{hookBaseName}MutationOptions"),
            returnType: null,
            parameters: new[]
            {
                new ParameterDeclarationNode(
                    new IdentifierNode("options"),
                    new RawTypeNode(mutationOptionsType),
                    isRequired: false,
                    documentation: new CommentNode("mutation: UseMutationOptions, request: AxiosRequestConfig", CommentKind.JSDoc))
            }.ToImmutableList(),
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Options for useMutation. Contains mutationKey and mutationFn.", CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildUseMutationFn(
        IrOperation operation,
        string hookBaseName,
        string variablesType)
    {
        var body = new BlockNode(new StatementNode[]
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("mutationOptions"),
                null,
                new CallExpressionNode(new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}MutationOptions")), new[] { new IdentifierExpressionNode(new IdentifierNode("options")) }.ToImmutableList())),
            new ReturnStatementNode(new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("useMutation")),
                new[] { new IdentifierExpressionNode(new IdentifierNode("mutationOptions")), new IdentifierExpressionNode(new IdentifierNode("queryClient")) }.ToImmutableList()))
        }.ToImmutableList());

        return new FunctionDeclarationNode(
            new IdentifierNode($"use{hookBaseName}"),
            returnType: null,
            parameters: new ParameterDeclarationNode[]
            {
                new ParameterDeclarationNode(
                    new IdentifierNode("options"),
                    new RawTypeNode($"Parameters<typeof get{hookBaseName}MutationOptions>[1]"),
                    isRequired: false,
                    documentation: new CommentNode("mutation: UseMutationOptions, request: AxiosRequestConfig", CommentKind.JSDoc)),
                new ParameterDeclarationNode(
                    new IdentifierNode("queryClient"),
                    new SimpleTypeNode(new IdentifierNode("QueryClient"), isPrimitive: false),
                    isRequired: false,
                    documentation: new CommentNode("Uses default QueryClient if not provided", CommentKind.JSDoc))
            }.ToImmutableList(),
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Mutation hook. Returns mutate function and status.", CommentKind.JSDoc),
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
            returnType: null,
            parameters: ImmutableList<ParameterDeclarationNode>.Empty,
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode("Returns true if this mutation is in progress.", CommentKind.JSDoc),
            isExported: true);
    }
}
