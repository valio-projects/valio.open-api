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

    public TanStackQueryTypeBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
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

    public TypeNode BuildRequestOptionsType() =>
        Union(SimpleType("AxiosRequestConfig"), SimpleType("undefined", isPrimitive: true));

    public InterfaceDeclarationNode BuildMutationOptionsParamsInterface(
        string hookBaseName,
        TypeNode dataType,
        TypeNode variablesType)
    {
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
                    SimpleType("AxiosRequestConfig"),
                    isRequired: false)
            }.ToImmutableList());
    }

    public InterfaceDeclarationNode BuildMutationOptionsResultInterface(
        string hookBaseName,
        TypeNode dataType,
        TypeNode variablesType)
    {
        var mutationFnType = GenericType("MutationFunction", dataType, SimpleType("Error"), variablesType);
        var mutationKeyType = ReadonlyArray(SimpleType("string", isPrimitive: true));
        return new InterfaceDeclarationNode(
            name: new IdentifierNode($"Get{hookBaseName}MutationOptionsResult"),
            accessModifier: AccessModifier.Public,
            baseTypes: ImmutableList<TypeNode>.Empty,
            members: new List<DeclarationNode>
            {
                new PropertySignatureNode(
                    new IdentifierNode("mutationKey"),
                    mutationKeyType,
                    isRequired: true),
                new PropertySignatureNode(
                    new IdentifierNode("mutationFn"),
                    mutationFnType,
                    isRequired: true)
            }.ToImmutableList());
    }

    public InterfaceDeclarationNode BuildQueryOptionsParamsInterface(
        string hookBaseName,
        TypeNode dataType,
        bool isInfinite)
    {
        var queryKeyType = BuildQueryKeyReturnType();
        TypeNode queryPropType = isInfinite
            ? Partial(GenericType("UseInfiniteQueryOptions", dataType, SimpleType("Error"), InfiniteData(dataType), queryKeyType, SimpleType("number", isPrimitive: true)))
            : Partial(GenericType("UseQueryOptions", dataType, SimpleType("Error"), dataType, dataType, queryKeyType));
        return new InterfaceDeclarationNode(
            name: new IdentifierNode($"Get{hookBaseName}{(isInfinite ? "Infinite" : "")}QueryOptionsParams"),
            accessModifier: AccessModifier.Public,
            baseTypes: ImmutableList<TypeNode>.Empty,
            members: new List<DeclarationNode>
            {
                new PropertySignatureNode(
                    new IdentifierNode("query"),
                    queryPropType,
                    isRequired: false),
                new PropertySignatureNode(
                    new IdentifierNode("request"),
                    SimpleType("AxiosRequestConfig"),
                    isRequired: false)
            }.ToImmutableList());
    }

    public TypeAliasDeclarationNode BuildQueryOptionsResultTypeAlias(
        string hookBaseName,
        TypeNode dataType,
        bool isInfinite)
    {
        var name = $"Get{hookBaseName}{(isInfinite ? "Infinite" : "")}QueryOptionsResult";
        var queryKeyType = BuildQueryKeyReturnType();
        var type = isInfinite
            ? GenericType("UseInfiniteQueryOptions", dataType, SimpleType("Error"), InfiniteData(dataType), queryKeyType, SimpleType("number", isPrimitive: true))
            : GenericType("UseQueryOptions", dataType, SimpleType("Error"), dataType, dataType, queryKeyType);
        return new TypeAliasDeclarationNode(new IdentifierNode(name), type);
    }

    /// <summary>UseQueryOptions для getXxxQueryOptions (queryKey из getXxxQueryKey)</summary>
    public TypeNode BuildUseQueryOptionsType(TypeNode dataType) =>
        GenericType("UseQueryOptions", dataType, SimpleType("Error"), dataType, dataType, BuildQueryKeyReturnType());

    /// <summary>UseInfiniteQueryOptions для getXxxInfiniteQueryOptions (TQueryFnData=page, TPageParam=number)</summary>
    public TypeNode BuildUseInfiniteQueryOptionsType(TypeNode dataType)
    {
        return GenericType("UseInfiniteQueryOptions", dataType, SimpleType("Error"), InfiniteData(dataType), BuildQueryKeyReturnType(), SimpleType("number", isPrimitive: true));
    }

    public TypeNode BuildUseQueryResultType(TypeNode dataType) =>
        GenericType("UseQueryResult", dataType, SimpleType("Error"));

    public TypeNode BuildUseInfiniteQueryResultType(TypeNode dataType) =>
        GenericType("UseInfiniteQueryResult", InfiniteData(dataType), SimpleType("Error"));

    public TypeNode BuildUseMutationResultType(TypeNode dataType, TypeNode variablesType) =>
        GenericType("UseMutationResult", dataType, SimpleType("Error"), variablesType);

    public TypeNode BuildPromiseQueryClientType() =>
        GenericType("Promise", SimpleType("QueryClient"));
}
