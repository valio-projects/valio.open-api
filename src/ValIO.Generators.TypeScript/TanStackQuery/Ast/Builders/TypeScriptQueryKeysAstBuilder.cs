using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.JsDoc;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;

/// <summary>
/// Builds the framework-agnostic <c>{op}.query-keys.ts</c> module: the single source of an operation's cache
/// identity, shared by the TanStack Query hooks (array query keys) and the Next.js transport (string cache tags).
/// </summary>
/// <remarks>
/// <para>
/// Emitting keys here — rather than inline in each <c>{op}.query.ts</c> — lets a hybrid app share one identity
/// between client components (axios + TanStack Query, importing <c>get…QueryKey</c>) and server components
/// (Next.js <c>fetch</c>, importing <c>get…CacheTags</c>). The file imports only the params type, so it carries
/// no framework dependency and is safe to import from either side.
/// </para>
/// <para>
/// Which exports are emitted is driven by the active consumers: the query-key functions when a TanStack wrapper
/// is selected, the cache-tag helpers when the Next.js transport is selected (both when it is a hybrid run).
/// </para>
/// </remarks>
/// <seealso cref="TypeScriptCacheKey"/>
/// <seealso cref="TypeScriptParameterModel"/>
public sealed class TypeScriptQueryKeysAstBuilder
{
    /// <summary>Naming converter used to derive PascalCase hook base names.</summary>
    private readonly INamingConverter _namingConverter;

    /// <summary>Initializes the builder.</summary>
    /// <param name="namingConverter">The naming converter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="namingConverter"/> is <see langword="null"/>.</exception>
    public TypeScriptQueryKeysAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    /// <summary>
    /// Builds the <c>{op}.query-keys.ts</c> source file for one operation.
    /// </summary>
    /// <param name="operation">The (non-mutation) IR operation.</param>
    /// <param name="operationFileName">The operation's file-name stem (camelCase original method name).</param>
    /// <param name="includeQueryKeys">Whether to emit the TanStack query-key functions (a wrapper is selected).</param>
    /// <param name="includeCacheTags">Whether to emit the Next.js cache-tag helpers (the nextjs transport is selected).</param>
    /// <returns>The rendered source file node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is <see langword="null"/>.</exception>
    public SourceFileNode BuildQueryKeysSourceFile(
        IrOperation operation,
        string operationFileName,
        bool includeQueryKeys,
        bool includeCacheTags)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var hookBaseName = _namingConverter.ToPascalCase(operation.MethodName);
        var hasParams = HasParameters(operation);
        var paramsTypeName = hasParams ? $"{hookBaseName}Params" : null;
        var model = TypeScriptParameterModel.Create(operation);

        var declarations = new List<DeclarationNode>();

        if (includeQueryKeys)
        {
            declarations.Add(BuildKeyFn($"get{hookBaseName}InfiniteQueryKey", operation, model, hasParams, paramsTypeName, includeInfinite: true));
            declarations.Add(BuildKeyFn($"get{hookBaseName}QueryKey", operation, model, hasParams, paramsTypeName, includeInfinite: false));
        }

        if (includeCacheTags)
        {
            declarations.Add(BuildBroadTagConst(operationFileName));
            declarations.Add(BuildCacheTagsFn(operation, hookBaseName, operationFileName, model, hasParams, paramsTypeName));
        }

        var imports = new List<ImportNode>();
        if (hasParams && paramsTypeName != null)
            imports.Add(new ImportNode(
                items: new[] { new IdentifierNode(paramsTypeName) }.ToImmutableList(),
                from: $"./{operationFileName}.params",
                kind: ImportKind.Named,
                isTypeOnly: true));

        return new SourceFileNode(
            imports: imports,
            declarations: declarations,
            fileDocumentation: TypeScriptDoc.FileHeader(
                $"{operationFileName}.query-keys.ts",
                $"endpoints/{operationFileName}",
                $"Shared cache keys/tags for the {operation.MethodName} operation (TanStack Query + Next.js)."));
    }

    /// <summary>
    /// Builds one query-key factory function whose body returns the key array from <see cref="TypeScriptCacheKey"/>.
    /// </summary>
    /// <param name="name">The exported function name (e.g. <c>getGetPetByIdQueryKey</c>).</param>
    /// <param name="operation">The IR operation.</param>
    /// <param name="model">The parameter model.</param>
    /// <param name="hasParams">Whether the operation has params (drives the required <c>params</c> argument).</param>
    /// <param name="paramsTypeName">The params interface name, or <see langword="null"/> when absent.</param>
    /// <param name="includeInfinite">Whether this is the infinite-query key variant.</param>
    /// <returns>The exported arrow-function declaration.</returns>
    private FunctionDeclarationNode BuildKeyFn(
        string name,
        IrOperation operation,
        TypeScriptParameterModel model,
        bool hasParams,
        string? paramsTypeName,
        bool includeInfinite)
    {
        var elements = TypeScriptCacheKey.BuildKeyElements(operation, model, includeInfinite);

        // params is required: the key inlines params.route.id / params.query, which cannot be reached when undefined.
        var parameters = hasParams && paramsTypeName != null
            ? new[] { new ParameterDeclarationNode(new IdentifierNode("params"), new SimpleTypeNode(new IdentifierNode(paramsTypeName), isPrimitive: false), isRequired: true) }.ToImmutableList()
            : ImmutableList<ParameterDeclarationNode>.Empty;

        return new FunctionDeclarationNode(
            new IdentifierNode(name),
            returnType: new ArrayTypeNode(new SimpleTypeNode(new IdentifierNode("unknown"), isPrimitive: true), isReadOnly: true),
            parameters: parameters,
            body: new BlockNode(new StatementNode[]
            {
                new ReturnStatementNode(new ArrayLiteralExpressionNode(elements.ToImmutableList(), preferMultilineFormat: true))
            }.ToImmutableList()),
            isArrowFunction: true,
            isAsync: false,
            documentation: new CommentNode(includeInfinite ? "Query key for useInfiniteQuery." : "Query key for useQuery.", CommentKind.JSDoc),
            isExported: true);
    }

    /// <summary>
    /// Builds the broad, parameter-independent operation cache-tag constant
    /// (<c>export const {op}CacheTag = "{op}";</c>).
    /// </summary>
    /// <param name="operationFileName">The camelCase operation file-name stem, used as both name stem and value.</param>
    /// <returns>The exported const declaration.</returns>
    private static VariableDeclarationNode BuildBroadTagConst(string operationFileName) =>
        new(
            name: new IdentifierNode($"{operationFileName}CacheTag"),
            declarationKind: VariableDeclarationKind.Const,
            initializer: new LiteralExpressionNode(operationFileName, LiteralKind.String),
            documentation: new CommentNode("Broad Next.js cache tag for this operation (revalidates all of its results).", CommentKind.JSDoc),
            isExported: true);

    /// <summary>
    /// Builds <c>get{Hook}CacheTags(params)</c>: returns the broad operation tag followed by the hierarchical
    /// path-prefix tags derived from the same segments as the query key, so server-side <c>revalidateTag</c>
    /// mirrors client-side prefix <c>invalidateQueries</c>.
    /// </summary>
    /// <param name="operation">The IR operation.</param>
    /// <param name="hookBaseName">The PascalCase hook base name.</param>
    /// <param name="operationFileName">The camelCase operation file-name stem (broad-tag const name stem).</param>
    /// <param name="model">The parameter model.</param>
    /// <param name="hasParams">Whether the operation has params.</param>
    /// <param name="paramsTypeName">The params interface name, or <see langword="null"/> when absent.</param>
    /// <returns>The exported arrow-function declaration returning <c>string[]</c>.</returns>
    private FunctionDeclarationNode BuildCacheTagsFn(
        IrOperation operation,
        string hookBaseName,
        string operationFileName,
        TypeScriptParameterModel model,
        bool hasParams,
        string? paramsTypeName)
    {
        var elements = new List<ExpressionNode>
        {
            new IdentifierExpressionNode(new IdentifierNode($"{operationFileName}CacheTag")),
        };
        elements.AddRange(TypeScriptCacheKey.BuildTagExpressions(operation, model));

        var parameters = hasParams && paramsTypeName != null
            ? new[] { new ParameterDeclarationNode(new IdentifierNode("params"), new SimpleTypeNode(new IdentifierNode(paramsTypeName), isPrimitive: false), isRequired: true) }.ToImmutableList()
            : ImmutableList<ParameterDeclarationNode>.Empty;

        return new FunctionDeclarationNode(
            new IdentifierNode($"get{hookBaseName}CacheTags"),
            returnType: new ArrayTypeNode(new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true)),
            parameters: parameters,
            body: new BlockNode(new StatementNode[]
            {
                new ReturnStatementNode(new ArrayLiteralExpressionNode(elements.ToImmutableList(), preferMultilineFormat: true))
            }.ToImmutableList()),
            isArrowFunction: true,
            isAsync: false,
            documentation: new CommentNode("Next.js cache tags for this operation (broad tag + hierarchical resource tags; use with revalidateTag).", CommentKind.JSDoc),
            isExported: true);
    }

    /// <summary>Mirror of <see cref="TypeScriptParamsAstBuilder.HasParameters"/> without a type-builder dependency.</summary>
    /// <param name="operation">The IR operation.</param>
    /// <returns><see langword="true"/> when the operation has any route/query/header parameters or a request body.</returns>
    private static bool HasParameters(IrOperation operation) =>
        operation.PathParameters.Count > 0
        || operation.QueryParameters.Count > 0
        || operation.HeaderParameters.Count > 0
        || operation.RequestBody != null;
}
