using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Generators.TypeScript.Ast.Nodes;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;

/// <summary>
/// Строит TypeNode и DeclarationNode для TanStack Query hook типов.
/// Использует AST-узлы вместо строк.
/// </summary>
public sealed class TanStackQueryTypeBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly bool _hookUsesAxios;

    public TanStackQueryTypeBuilder(INamingConverter namingConverter, bool hookUsesAxios = true)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _hookUsesAxios = hookUsesAxios;
    }

    /// <summary>SimpleTypeNode для примитива или идентификатора</summary>
    public static TypeNode SimpleType(string name, bool isPrimitive = false) =>
        new SimpleTypeNode(new IdentifierNode(name), isPrimitive);

    /// <summary>GenericTypeNode: Name&lt;T1, T2, ...&gt;</summary>
    public static TypeNode GenericType(string name, params TypeNode[] typeArguments) =>
        new GenericTypeNode(
            new IdentifierNode(name),
            typeArguments.ToImmutableList());

    /// <summary>ArrayTypeNode: readonly T[]</summary>
    public static TypeNode ReadonlyArray(TypeNode elementType) =>
        new ArrayTypeNode(elementType, isReadOnly: true);

    /// <summary>OptionalTypeNode: T | undefined</summary>
    public static TypeNode Optional(TypeNode inner) =>
        new OptionalTypeNode(inner);

    /// <summary>UnionTypeNode: T1 | T2</summary>
    public static TypeNode Union(params TypeNode[] types) =>
        new UnionTypeNode(types.ToImmutableList());

    /// <summary>Partial&lt;T&gt;</summary>
    public static TypeNode Partial(TypeNode inner) =>
        GenericType("Partial", inner);

    /// <summary>InfiniteData&lt;T&gt;</summary>
    public static TypeNode InfiniteData(TypeNode inner) =>
        GenericType("InfiniteData", inner);

    public TypeNode BuildDataType(string? resultTypeName) =>
        resultTypeName != null ? SimpleType(resultTypeName) : SimpleType("void", isPrimitive: true);

    public TypeNode BuildVariablesType(string? paramsTypeName) =>
        paramsTypeName != null ? SimpleType(paramsTypeName) : SimpleType("void", isPrimitive: true);

    public TypeNode BuildQueryKeyReturnType() =>
        ReadonlyArray(SimpleType("unknown", isPrimitive: true));

    /// <summary>QueryFunctionContext для обычного query (queryKey из getXxxQueryKey)</summary>
    public TypeNode BuildQueryFunctionContextType() =>
        GenericType("QueryFunctionContext", BuildQueryKeyReturnType());

    /// <summary>QueryFunctionContext для infinite query (queryKey + pageParam, initialPageParam: 0)</summary>
    public TypeNode BuildQueryFunctionContextForInfiniteType() =>
        GenericType("QueryFunctionContext", BuildQueryKeyReturnType(), SimpleType("number", isPrimitive: true));

    /// <summary>MutationFunctionContext для mutationFn (variables, context) => Promise&lt;TData&gt;</summary>
    public TypeNode BuildMutationFunctionContextType() =>
        SimpleType("MutationFunctionContext");

    public TypeNode BuildRequestOptionsType() =>
        Union(
            SimpleType(_hookUsesAxios ? "AxiosRequestConfig" : "RequestInit"),
            SimpleType("undefined", isPrimitive: true));

    public InterfaceDeclarationNode BuildMutationOptionsParamsInterface(
        string hookBaseName,
        TypeNode dataType,
        TypeNode variablesType)
    {
        // UseMutationOptions<TData, TError, TVariables, TOnMutateResult>
        var mutationOptionsType = GenericType("UseMutationOptions", dataType, SimpleType("Error"), variablesType);
        return new InterfaceDeclarationNode(
            name: new IdentifierNode($"Get{hookBaseName}MutationOptionsParams"),
            accessModifier: AccessModifier.Public,
            baseTypes: ImmutableList<TypeNode>.Empty,
            members: new List<DeclarationNode>
            {
                new PropertySignatureNode(
                    new IdentifierNode("mutation"),
                    mutationOptionsType,
                    isRequired: false),
                new PropertySignatureNode(
                    new IdentifierNode("request"),
                    SimpleType(_hookUsesAxios ? "AxiosRequestConfig" : "RequestInit"),
                    isRequired: false)
            }.ToImmutableList());
    }

    /// <summary>
    /// Builds the <c>Get{hookBaseName}MutationOptionsResult</c> type alias — the full
    /// <c>UseMutationOptions&lt;TData, Error, TVariables&gt;</c> object returned by <c>get…MutationOptions</c>.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="BuildQueryOptionsResultTypeAlias"/>: the factory returns the complete options object
    /// (the caller's <c>options.mutation</c> merged with the generated <c>mutationKey</c>/<c>mutationFn</c>).
    /// The result therefore has to be the full <c>UseMutationOptions</c> and not a narrow
    /// <c>{ mutationKey; mutationFn }</c> shape — otherwise the caller's <c>onSuccess</c>/<c>onError</c>/
    /// <c>retry</c>/… would be dropped at the type level even though they are present at runtime (BUG #11).
    /// </remarks>
    /// <param name="hookBaseName">The PascalCase hook base name (e.g. <c>AddPet</c>).</param>
    /// <param name="dataType">The mutation's <c>TData</c> type node.</param>
    /// <param name="variablesType">The mutation's <c>TVariables</c> type node.</param>
    /// <returns>The exported <c>Get{hookBaseName}MutationOptionsResult</c> type alias declaration.</returns>
    /// <seealso cref="BuildMutationOptionsParamsInterface"/>
    public TypeAliasDeclarationNode BuildMutationOptionsResultTypeAlias(
        string hookBaseName,
        TypeNode dataType,
        TypeNode variablesType)
    {
        // UseMutationOptions<TData, TError, TVariables> — TContext defaults to unknown, matching the params interface.
        var type = GenericType("UseMutationOptions", dataType, SimpleType("Error"), variablesType);
        return new TypeAliasDeclarationNode(new IdentifierNode($"Get{hookBaseName}MutationOptionsResult"), type);
    }

    /// <summary>Declares the options override with a selectable observer data type.</summary>
    /// <param name="hookBaseName">The PascalCase operation name.</param>
    /// <param name="dataType">The wire response envelope.</param>
    /// <param name="isInfinite">Whether the observer reads accumulated pages.</param>
    /// <param name="additionalMembers">Operation-specific options beside the query and request options.</param>
    /// <returns>The options interface with its default observer data type.</returns>
    public InterfaceDeclarationNode BuildQueryOptionsParamsInterface(
        string hookBaseName,
        TypeNode dataType,
        bool isInfinite,
        IReadOnlyList<DeclarationNode>? additionalMembers = null)
    {
        var selectedData = SimpleType("TData");
        var queryType = isInfinite ? BuildUseInfiniteQueryOptionsType(dataType, selectedData) : BuildUseQueryOptionsType(dataType, selectedData);
        var members = new List<DeclarationNode>
        {
            new PropertySignatureNode(new IdentifierNode("query"), Partial(queryType), isRequired: false),
            new PropertySignatureNode(
                new IdentifierNode("request"),
                SimpleType(_hookUsesAxios ? "AxiosRequestConfig" : "RequestInit"),
                isRequired: false)
        };
        if (additionalMembers != null)
            members.AddRange(additionalMembers);

        return new GenericInterfaceDeclarationNode(
            name: new IdentifierNode($"Get{hookBaseName}{(isInfinite ? "Infinite" : "")}QueryOptionsParams"),
            genericParameters: BuildQueryDataParameters(dataType, isInfinite),
            members: members);
    }

    /// <summary>Declares the full TanStack options returned by an operation's options factory.</summary>
    /// <param name="hookBaseName">The PascalCase operation name.</param>
    /// <param name="dataType">The wire response envelope.</param>
    /// <param name="isInfinite">Whether the observer reads accumulated pages.</param>
    /// <returns>The result alias with its own scoped observer data parameter.</returns>
    public TypeAliasDeclarationNode BuildQueryOptionsResultTypeAlias(
        string hookBaseName,
        TypeNode dataType,
        bool isInfinite)
    {
        var name = $"Get{hookBaseName}{(isInfinite ? "Infinite" : "")}QueryOptionsResult";
        var selectedData = SimpleType("TData");
        var type = isInfinite ? BuildUseInfiniteQueryOptionsType(dataType, selectedData) : BuildUseQueryOptionsType(dataType, selectedData);
        return new TypeAliasDeclarationNode(new IdentifierNode(name), type, genericParameters: BuildQueryDataParameters(dataType, isInfinite));
    }

    /// <summary>Declares the selected value, defaulting to the wire response or the accumulated pages.</summary>
    /// <param name="dataType">The wire response envelope.</param>
    /// <param name="isInfinite">Whether the observer reads accumulated pages.</param>
    /// <returns>The single declaration for the observer's <c>TData</c>.</returns>
    public static IReadOnlyList<GenericParameterNode> BuildQueryDataParameters(TypeNode dataType, bool isInfinite = false) =>
        [new(new IdentifierNode("TData"), @default: isInfinite ? InfiniteData(dataType) : dataType)];

    /// <summary>Builds ordinary query options with independent wire and selected data types.</summary>
    /// <param name="dataType">The wire response envelope used by the query function and cache callbacks.</param>
    /// <param name="selectedDataType">The observer projection, or null to keep the wire envelope.</param>
    /// <returns>The TanStack ordinary query options type.</returns>
    public TypeNode BuildUseQueryOptionsType(TypeNode dataType, TypeNode? selectedDataType = null) =>
        GenericType("UseQueryOptions", dataType, SimpleType("Error"), selectedDataType ?? dataType, BuildQueryKeyReturnType());

    /// <summary>Builds infinite options with numeric page parameters and independent observer data.</summary>
    /// <param name="dataType">The wire response envelope for one page.</param>
    /// <param name="selectedDataType">The observer projection, or null to keep the accumulated pages.</param>
    /// <returns>The TanStack infinite query options type.</returns>
    public TypeNode BuildUseInfiniteQueryOptionsType(TypeNode dataType, TypeNode? selectedDataType = null) =>
        GenericType("UseInfiniteQueryOptions", dataType, SimpleType("Error"), selectedDataType ?? InfiniteData(dataType), BuildQueryKeyReturnType(), SimpleType("number", isPrimitive: true));

    /// <summary>Builds the ordinary observer result exposed to the hook caller.</summary>
    /// <param name="dataType">The observer data type after any selection.</param>
    /// <returns>The TanStack ordinary query result type.</returns>
    public TypeNode BuildUseQueryResultType(TypeNode dataType) =>
        GenericType("UseQueryResult", dataType, SimpleType("Error"));

    /// <summary>Builds the infinite observer result exposed to the hook caller.</summary>
    /// <param name="dataType">The wire response envelope for one page.</param>
    /// <param name="selectedDataType">The observer projection, or null to keep the accumulated pages.</param>
    /// <returns>The TanStack infinite query result type.</returns>
    public TypeNode BuildUseInfiniteQueryResultType(TypeNode dataType, TypeNode? selectedDataType = null) =>
        GenericType("UseInfiniteQueryResult", selectedDataType ?? InfiniteData(dataType), SimpleType("Error"));

    public TypeNode BuildUseMutationResultType(TypeNode dataType, TypeNode variablesType) =>
        GenericType("UseMutationResult", dataType, SimpleType("Error"), variablesType);

    public TypeNode BuildPromiseQueryClientType() =>
        GenericType("Promise", SimpleType("QueryClient"));
}
