using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.JsDoc;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Nextjs.Ast.Builders;

/// <summary>
/// Builds a Next.js (App Router) <c>fetch</c> client for an IR operation as TypeScript AST,
/// emitting one server-safe <c>{op}.nextjs.ts</c> per operation.
/// </summary>
/// <remarks>
/// Unlike the plain fetch client this transport is built for React Server Components / Server Actions:
/// it emits <b>no</b> <c>"use client"</c> directive, types its options as
/// <c>RequestInit &amp; { next?: { revalidate?, tags? } }</c> (the Next.js fetch augmentation), and for
/// cacheable <c>GET</c> operations attaches a per-operation cache <b>tag</b> plus an optional
/// <c>revalidate&lt;Op&gt;()</c> helper that calls <c>revalidateTag(tag, "max")</c> from <c>next/cache</c>
/// for on-demand invalidation. Caller-supplied <c>options</c> (cache, credentials, headers, next-config)
/// are merged in, with per-field override. Everything else — the parameter/result types, schemas,
/// validation and the discriminable <c>{Op}Results</c> union — is shared verbatim with the other transports.
/// </remarks>
public sealed class NextjsClientAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptParamsAstBuilder _paramsBuilder;
    private readonly TypeScriptResultsAstBuilder _resultsBuilder;
    private readonly Validation.IClientValidationStrategy _validation;
    private readonly int? _defaultRevalidate;
    private readonly bool _generateRevalidateHelpers;

    /// <summary>Map «affix name → original name» so import paths use the un-affixed name.</summary>
    private readonly Dictionary<string, string> _orig = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates the builder, resolving the validation strategy and the Next.js-specific options.
    /// </summary>
    /// <param name="namingConverter">The naming converter.</param>
    /// <param name="options">The TypeScript generation options (validation library, Next.js settings, etc.).</param>
    public NextjsClientAstBuilder(INamingConverter namingConverter, TypeScriptGenerationOptions? options)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _validation = Validation.ClientValidationStrategyFactory.Create(options);

        var typeBuilder = new TypeScriptAstBuilder(namingConverter, options);
        _paramsBuilder = new TypeScriptParamsAstBuilder(namingConverter, typeBuilder.BuildType);
        _resultsBuilder = new TypeScriptResultsAstBuilder(namingConverter, typeBuilder.BuildType, options?.ResultTypeSuffix, options?.ResultTypePrefix);

        _defaultRevalidate = options?.NextJs?.DefaultRevalidate;
        _generateRevalidateHelpers = options?.NextJs?.GenerateRevalidateHelpers ?? true;
    }

    /// <summary>
    /// Builds the <c>{op}.nextjs.ts</c> source file for one operation: the client function plus, for a
    /// cacheable GET, a cache-tag constant and (optionally) a <c>revalidate&lt;Op&gt;()</c> helper.
    /// </summary>
    /// <param name="operation">The IR operation.</param>
    /// <param name="fileOrganization">File-layout options (paths, separate-models).</param>
    /// <returns>The rendered source file node.</returns>
    public SourceFileNode BuildOperationClientSourceFile(IrOperation operation, FileOrganization fileOrganization)
    {
        ArgumentNullException.ThrowIfNull(operation);
        OriginalNameMap.AddOperation(_orig, operation);

        var operationFileName = _namingConverter.ToCamelCase(operation.OriginalMethodName);
        var hasParameters = _paramsBuilder.HasParameters(operation);
        var hasResultType = _resultsBuilder.HasResponsesWithBody(operation);
        // Only GET responses participate in Next's persistent cache, so tags/revalidate apply to GET only.
        var isCacheable = string.Equals(operation.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase);
        var cacheTagName = $"{operationFileName}CacheTag";

        var declarations = ImmutableList.CreateBuilder<DeclarationNode>();
        if (isCacheable)
            declarations.Add(BuildCacheTagConst(cacheTagName, operationFileName));

        declarations.Add(BuildOperationFunction(operation, hasParameters, hasResultType, isCacheable, cacheTagName));

        if (isCacheable && _generateRevalidateHelpers)
            declarations.Add(BuildRevalidateHelper(operation, cacheTagName));

        return new SourceFileNode(
            imports: BuildImports(operation, operationFileName, fileOrganization, hasParameters, hasResultType, isCacheable),
            declarations: declarations.ToImmutable(),
            fileDocumentation: TypeScriptDoc.FileHeader(
                $"{operationFileName}.nextjs.ts",
                $"endpoints/{operationFileName}",
                $"Next.js (App Router) fetch client for the {operation.MethodName} operation."));
    }

    private IReadOnlyList<ImportNode> BuildImports(
        IrOperation operation,
        string operationFileName,
        FileOrganization fileOrganization,
        bool hasParameters,
        bool hasResultType,
        bool isCacheable)
    {
        var imports = ImmutableList.CreateBuilder<ImportNode>();

        // No transport import — `fetch` is global. Only the revalidate helper needs next/cache.
        if (isCacheable && _generateRevalidateHelpers)
            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode("revalidateTag")),
                from: "next/cache",
                kind: ImportKind.Named));

        // Validation runtime imports (z / v / Type / Value), driven by the generated expressions.
        imports.AddRange(_validation.RuntimeImports(
            ClientInlineConstructedTypes(operation),
            hasParams: operation.PathParameters.Count > 0 || operation.QueryParameters.Count > 0,
            hasNamedSchemaParse: Validation.ClientResponseSchemas.ValidatedResponses(operation).Any()));

        if (hasParameters)
            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(_paramsBuilder.ParamsInterfaceName(operation.MethodName))),
                from: $"./{operationFileName}.params",
                kind: ImportKind.Named,
                isTypeOnly: true));

        if (hasResultType)
            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(_resultsBuilder.ResultTypeName(operation.MethodName))),
                from: $"./{operationFileName}.results",
                kind: ImportKind.Named,
                isTypeOnly: true));

        if (_validation.IsEnabled)
        {
            AddParamsSchemaImports(imports, operation, operationFileName);

            foreach (var modelName in Validation.ClientResponseSchemas.ModelSchemaNames(operation))
                imports.Add(new ImportNode(
                    items: ImmutableList.Create(new IdentifierNode($"{modelName}Schema")),
                    from: BuildSchemaImportPath(modelName, fileOrganization),
                    kind: ImportKind.Named));

            var resultSchemaNames = Validation.ClientResponseSchemas.ConstructedResultSchemaNames(_namingConverter, operation);
            if (resultSchemaNames.Count > 0)
                imports.Add(new ImportNode(
                    items: resultSchemaNames.Select(n => new IdentifierNode(n)).ToImmutableList(),
                    from: $"./{operationFileName}.results.schemas",
                    kind: ImportKind.Named));
        }

        return imports.ToImmutable();
    }

    private void AddParamsSchemaImports(ImmutableList<ImportNode>.Builder imports, IrOperation operation, string operationFileName)
    {
        var opName = _namingConverter.ToPascalCase(operation.MethodName);
        var names = new List<IdentifierNode>();
        if (operation.PathParameters.Count > 0)
            names.Add(new IdentifierNode($"{opName}RouteParamsSchema"));
        if (operation.QueryParameters.Count > 0)
            names.Add(new IdentifierNode($"{opName}QueryParamsSchema"));
        if (names.Count > 0)
            imports.Add(new ImportNode(
                items: names.ToImmutableList(),
                from: $"./{operationFileName}.params.schema",
                kind: ImportKind.Named));
    }

    private static IReadOnlyCollection<IrType> ClientInlineConstructedTypes(IrOperation operation) =>
        operation.RequestBody != null && Validation.ValidationTypes.IsValidatable(operation.RequestBody.Type)
            ? new[] { operation.RequestBody.Type }
            : Array.Empty<IrType>();

    /// <summary>Builds <c>export const {op}CacheTag = "{tag}";</c> — the per-operation Next.js cache tag.</summary>
    private static DeclarationNode BuildCacheTagConst(string cacheTagName, string tagValue) =>
        new VariableDeclarationNode(
            name: new IdentifierNode(cacheTagName),
            declarationKind: VariableDeclarationKind.Const,
            initializer: String(tagValue),
            documentation: new CommentNode("Next.js cache tag for this operation (use with revalidateTag).", CommentKind.JSDoc),
            isExported: true);

    /// <summary>Builds <c>export function revalidate{Op}() { revalidateTag({op}CacheTag, "max"); }</c>.</summary>
    private FunctionDeclarationNode BuildRevalidateHelper(IrOperation operation, string cacheTagName)
    {
        var call = new ExpressionStatementNode(new CallExpressionNode(
            Id("revalidateTag"),
            ImmutableList.Create<ExpressionNode>(Id(cacheTagName), String("max"))));
        return new FunctionDeclarationNode(
            name: new IdentifierNode($"revalidate{_namingConverter.ToPascalCase(operation.MethodName)}"),
            returnType: Simple("void", isPrimitive: true),
            parameters: ImmutableList<ParameterDeclarationNode>.Empty,
            body: new BlockNode(ImmutableList.Create<StatementNode>(call)),
            isAsync: false,
            documentation: new CommentNode(
                $"On-demand invalidation of the {operation.MethodName} cache. Call from a Server Action or Route Handler.",
                CommentKind.JSDoc),
            isExported: true);
    }

    private FunctionDeclarationNode BuildOperationFunction(
        IrOperation operation,
        bool hasParameters,
        bool hasResultType,
        bool isCacheable,
        string cacheTagName)
    {
        var returnType = hasResultType
            ? Simple(_resultsBuilder.ResultTypeName(operation.MethodName))
            : Simple("void", isPrimitive: true);

        var parameters = ImmutableList.CreateBuilder<ParameterDeclarationNode>();
        if (hasParameters)
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("params"),
                type: Simple(_paramsBuilder.ParamsInterfaceName(operation.MethodName)),
                isRequired: true));

        // Next.js fetch options: RequestInit augmented with { next?: { revalidate, tags } }.
        parameters.Add(new ParameterDeclarationNode(
            name: new IdentifierNode("options"),
            type: BuildOptionsType(),
            isRequired: false,
            documentation: new CommentNode("Optional Next.js fetch options (cache, next.revalidate, next.tags, headers, …).", CommentKind.JSDoc)));

        parameters.Add(new ParameterDeclarationNode(
            name: new IdentifierNode("signal"),
            type: Simple("AbortSignal"),
            isRequired: false));

        return new FunctionDeclarationNode(
            name: new IdentifierNode(operation.MethodName),
            returnType: new GenericTypeNode(new IdentifierNode("Promise"), ImmutableList.Create(returnType)),
            parameters: parameters.ToImmutable(),
            body: BuildOperationFunctionBody(operation, hasResultType, isCacheable, cacheTagName),
            isAsync: true,
            documentation: BuildDocumentation(operation),
            isExported: true);
    }

    private BlockNode BuildOperationFunctionBody(IrOperation operation, bool hasResultType, bool isCacheable, string cacheTagName)
    {
        var statements = new List<StatementNode>();
        var sources = BuildParameterSources(operation);

        if (_validation.IsEnabled)
            AddValidationParsingStatements(operation, sources, statements);

        // fetch has no structured `query` option → build a URLSearchParams and fold it into the URL.
        var urlExpression = BuildUrlWithQuery(operation, sources, statements);

        statements.Add(new VariableDeclarationStatementNode(new IdentifierNode("requestUrl"), type: null, initializer: urlExpression));
        statements.Add(new BlankLineStatementNode());

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("requestInit"),
            type: null,
            initializer: BuildFetchInit(operation, sources, isCacheable, cacheTagName)));
        statements.Add(new BlankLineStatementNode());

        var fetchCall = new CallExpressionNode(
            Id("fetch"),
            ImmutableList.Create<ExpressionNode>(Id("requestUrl"), Id("requestInit")));

        if (!hasResultType)
        {
            statements.Add(new ExpressionStatementNode(new AwaitExpressionNode(fetchCall)));
            return new BlockNode(statements.ToImmutableList());
        }

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("response"), type: null, initializer: new AwaitExpressionNode(fetchCall)));

        // responseData = response.status === 204 ? undefined : await response.json() as unknown
        var bodyJson = new CastExpressionNode(
            Simple("unknown", isPrimitive: true),
            new AwaitExpressionNode(new CallExpressionNode(Member(Id("response"), "json"), ImmutableList<ExpressionNode>.Empty)),
            useAsOperator: true);
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("responseData"),
            type: null,
            initializer: new ConditionalExpressionNode(
                new BinaryExpressionNode(Member(Id("response"), "status"), BinaryOperator.Equal, Number(204)),
                Id("undefined"),
                bodyJson)));
        statements.Add(new BlankLineStatementNode());

        AddStatusResultBranches(operation, statements);
        statements.Add(BuildFallbackResult(operation));

        return new BlockNode(statements.ToImmutableList());
    }

    /// <summary>
    /// Builds the request URL expression. When query params exist, emits a <c>URLSearchParams</c> populated
    /// from the params and returns <c>`{path}` + (query.size > 0 ? `?${query}` : "")</c>; otherwise the path.
    /// </summary>
    private ExpressionNode BuildUrlWithQuery(IrOperation operation, ParameterSources sources, List<StatementNode> statements)
    {
        var pathExpression = BuildUrlExpression(operation, sources);
        if (operation.QueryParameters.Count == 0 || sources.QuerySource == null)
            return pathExpression;

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("query"),
            type: null,
            initializer: new CallExpressionNode(Id("URLSearchParams"), ImmutableList<ExpressionNode>.Empty, isNewExpression: true)));

        foreach (var parameter in operation.QueryParameters)
        {
            var access = BuildParameterAccess(sources.QuerySource, parameter.Name, sources.QuerySourceMayBeUndefined);
            // Scalars stringify directly; array values are comma-joined by String(...) — adequate for v1.
            var setCall = new ExpressionStatementNode(new CallExpressionNode(
                Member(Id("query"), "set"),
                ImmutableList.Create<ExpressionNode>(String(parameter.Name),
                    new CallExpressionNode(Id("String"), ImmutableList.Create<ExpressionNode>(access)))));
            if (parameter.IsRequired)
                statements.Add(setCall);
            else
                statements.Add(new IfStatementNode(
                    new BinaryExpressionNode(access, BinaryOperator.NotEqual, Id("undefined")),
                    new BlockNode(ImmutableList.Create<StatementNode>(setCall))));
        }
        statements.Add(new BlankLineStatementNode());

        var queryTail = new ConditionalExpressionNode(
            new BinaryExpressionNode(Member(Id("query"), "size"), BinaryOperator.GreaterThan, Number(0)),
            new TemplateLiteralExpressionNode(ImmutableList.Create(new TemplateLiteralPart("?"), new TemplateLiteralPart(Id("query")))),
            String(string.Empty));
        return new BinaryExpressionNode(pathExpression, BinaryOperator.Add, queryTail);
    }

    private void AddValidationParsingStatements(IrOperation operation, ParameterSources sources, List<StatementNode> statements)
    {
        var opName = _namingConverter.ToPascalCase(operation.MethodName);

        if (operation.PathParameters.Count > 0 && sources.RouteSource != null)
        {
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("parsedRoute"), type: null,
                initializer: _validation.ValidateNamed($"{opName}RouteParamsSchema", sources.RouteSource)));
            sources.RouteSource = Id("parsedRoute");
        }

        if (operation.QueryParameters.Count > 0 && sources.QuerySource != null)
        {
            var queryInput = sources.QuerySourceMayBeUndefined
                ? new BinaryExpressionNode(sources.QuerySource, BinaryOperator.NullishCoalescing, EmptyObject())
                : sources.QuerySource;
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("parsedQuery"), type: null,
                initializer: _validation.ValidateNamed($"{opName}QueryParamsSchema", queryInput)));
            sources.QuerySource = Id("parsedQuery");
            sources.QuerySourceMayBeUndefined = false;
        }

        if (operation.RequestBody != null
            && Validation.ValidationTypes.IsValidatable(operation.RequestBody.Type)
            && sources.BodySource != null)
        {
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("parsedBody"), type: null,
                initializer: _validation.Validate(operation.RequestBody.Type, sources.BodySource)));
            sources.BodySource = Id("parsedBody");
            sources.BodySourceMayBeUndefined = false;
        }

        if (statements.Count > 0)
            statements.Add(new BlankLineStatementNode());
    }

    /// <summary>
    /// Builds the fetch <c>RequestInit</c>: caller <c>options</c> first (so the fixed parts override them),
    /// then <c>method</c>, merged <c>headers</c>, JSON <c>body</c>, the Next <c>next</c> config (GET only,
    /// caller-overridable per field), and the optional <c>signal</c>.
    /// </summary>
    private ObjectLiteralExpressionNode BuildFetchInit(IrOperation operation, ParameterSources sources, bool isCacheable, string cacheTagName)
    {
        var props = new List<PropertyAssignmentNode>
        {
            // Caller options first; the operation-fixed parts below override them.
            SpreadProperty(new BinaryExpressionNode(Id("options"), BinaryOperator.NullishCoalescing, EmptyObject())),
            new(new IdentifierNode("method"), String(operation.HttpMethod.ToUpperInvariant())),
        };

        if (operation.HeaderParameters.Count > 0 && sources.HeadersSource != null)
        {
            // headers: { ...(paramHeaders ?? {}), ...((options?.headers as Record<string,string>) ?? {}) }
            var paramHeaders = new BinaryExpressionNode(
                new CastExpressionNode(RecordStringString(),
                    new CastExpressionNode(Simple("unknown", isPrimitive: true), sources.HeadersSource, useAsOperator: true), useAsOperator: true),
                BinaryOperator.NullishCoalescing, EmptyObject());
            var callerHeaders = new BinaryExpressionNode(
                new CastExpressionNode(
                    new UnionTypeNode(ImmutableList.Create<TypeNode>(RecordStringString(), Simple("undefined", isPrimitive: true))),
                    Member(Id("options"), "headers", optional: true), useAsOperator: true),
                BinaryOperator.NullishCoalescing, EmptyObject());
            props.Add(new PropertyAssignmentNode(new IdentifierNode("headers"),
                new ObjectLiteralExpressionNode(ImmutableList.Create(SpreadProperty(paramHeaders), SpreadProperty(callerHeaders)))));
        }

        if (operation.RequestBody != null && sources.BodySource != null)
        {
            ExpressionNode bodyExpression = operation.RequestBody.IsJson
                ? new CallExpressionNode(Member(Id("JSON"), "stringify"), ImmutableList.Create<ExpressionNode>(sources.BodySource))
                : new CastExpressionNode(Simple("string", isPrimitive: true),
                    new CastExpressionNode(Simple("unknown", isPrimitive: true), sources.BodySource, useAsOperator: true), useAsOperator: true);

            if (sources.BodySourceMayBeUndefined)
                props.Add(SpreadProperty(new ConditionalExpressionNode(
                    new BinaryExpressionNode(sources.BodySource, BinaryOperator.NotEqual, Id("undefined")),
                    Object(("body", bodyExpression)), EmptyObject())));
            else
                props.Add(new PropertyAssignmentNode(new IdentifierNode("body"), bodyExpression));
        }

        if (isCacheable)
            props.Add(new PropertyAssignmentNode(new IdentifierNode("next"), BuildNextConfig(cacheTagName)));

        props.Add(SpreadProperty(new ConditionalExpressionNode(Id("signal"), Object(("signal", Id("signal"))), EmptyObject())));

        return new ObjectLiteralExpressionNode(props.ToImmutableList(), preferMultilineFormat: true);
    }

    /// <summary>
    /// Builds the Next config object: <c>{ [revalidate: N,] tags: [{op}CacheTag], ...(options?.next ?? {}) }</c>.
    /// The trailing spread lets the caller override <c>revalidate</c>/<c>tags</c> per field.
    /// </summary>
    private ObjectLiteralExpressionNode BuildNextConfig(string cacheTagName)
    {
        var nextProps = new List<PropertyAssignmentNode>();
        if (_defaultRevalidate.HasValue)
            nextProps.Add(new PropertyAssignmentNode(new IdentifierNode("revalidate"), Number(_defaultRevalidate.Value)));
        nextProps.Add(new PropertyAssignmentNode(new IdentifierNode("tags"),
            new ArrayLiteralExpressionNode(ImmutableList.Create<ExpressionNode>(Id(cacheTagName)))));
        nextProps.Add(SpreadProperty(new BinaryExpressionNode(
            Member(Id("options"), "next", optional: true), BinaryOperator.NullishCoalescing, EmptyObject())));
        return new ObjectLiteralExpressionNode(nextProps.ToImmutableList(), preferMultilineFormat: true);
    }

    private void AddStatusResultBranches(IrOperation operation, List<StatementNode> statements)
    {
        var resultTypeName = _resultsBuilder.ResultTypeName(operation.MethodName);
        var responseStatus = Member(Id("response"), "status");
        var responseData = Id("responseData");

        foreach (var response in operation.Responses.Where(r => r.Type != null && int.TryParse(r.StatusCode, out _)))
        {
            var statusCode = int.Parse(response.StatusCode);
            var isSuccessful = statusCode >= 200 && statusCode < 300;
            var body = BuildResponseBody(operation, response, responseData);

            var returnObject = new CastExpressionNode(
                Simple(resultTypeName),
                Object(
                    ("status", Number(statusCode)),
                    ("body", body),
                    ("isSuccessful", Bool(isSuccessful)),
                    ("isFailed", Bool(!isSuccessful))),
                useAsOperator: true);

            statements.Add(new IfStatementNode(
                new BinaryExpressionNode(responseStatus, BinaryOperator.Equal, Number(statusCode)),
                new BlockNode(ImmutableList.Create<StatementNode>(new ReturnStatementNode(returnObject)))));
        }
    }

    private ReturnStatementNode BuildFallbackResult(IrOperation operation)
    {
        var resultTypeName = _resultsBuilder.ResultTypeName(operation.MethodName);
        // Undocumented status → catch-all `…ResultOther`, a literal failure so the union stays discriminable.
        return new ReturnStatementNode(new CastExpressionNode(
            Simple(resultTypeName),
            Object(
                ("status", Member(Id("response"), "status")),
                ("body", Id("responseData")),
                ("isSuccessful", Bool(false)),
                ("isFailed", Bool(true))),
            useAsOperator: true));
    }

    private ExpressionNode BuildUrlExpression(IrOperation operation, ParameterSources sources)
    {
        if (operation.PathParameters.Count == 0 || sources.RouteSource == null)
            return String(operation.Path);

        var parts = new List<TemplateLiteralPart>();
        var cursor = 0;
        foreach (var parameter in operation.PathParameters)
        {
            var token = $"{{{parameter.Name}}}";
            var index = operation.Path.IndexOf(token, cursor, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;
            if (index > cursor)
                parts.Add(new TemplateLiteralPart(operation.Path[cursor..index]));
            parts.Add(new TemplateLiteralPart(BuildParameterAccess(sources.RouteSource, parameter.Name, sourceMayBeUndefined: false)));
            cursor = index + token.Length;
        }
        if (cursor < operation.Path.Length)
            parts.Add(new TemplateLiteralPart(operation.Path[cursor..]));

        return parts.Count == 0 ? String(operation.Path) : new TemplateLiteralExpressionNode(parts.ToImmutableList());
    }

    private ParameterSources BuildParameterSources(IrOperation operation)
    {
        var hasRoute = operation.PathParameters.Count > 0;
        var hasQuery = operation.QueryParameters.Count > 0;
        var hasHeaders = operation.HeaderParameters.Count > 0;
        var hasBody = operation.RequestBody != null;
        var groupCount = (hasRoute ? 1 : 0) + (hasQuery ? 1 : 0) + (hasHeaders ? 1 : 0) + (hasBody ? 1 : 0);
        var isFlat = groupCount <= 1;
        var paramsExpr = Id("params");

        return new ParameterSources
        {
            RouteSource = hasRoute ? isFlat ? paramsExpr : Member(paramsExpr, "route") : null,
            QuerySource = hasQuery ? isFlat ? paramsExpr : Member(paramsExpr, "query") : null,
            QuerySourceMayBeUndefined = hasQuery && !isFlat && operation.QueryParameters.All(p => !p.IsRequired),
            HeadersSource = hasHeaders ? isFlat ? paramsExpr : Member(paramsExpr, "headers") : null,
            HeadersSourceMayBeUndefined = hasHeaders && !isFlat,
            BodySource = hasBody ? BodyPropertyAccess(operation.RequestBody!, paramsExpr) : null,
            BodySourceMayBeUndefined = hasBody && operation.RequestBody?.IsRequired == false
        };
    }

    private static ExpressionNode BodyPropertyAccess(IrRequestBody requestBody, ExpressionNode source) =>
        Member(source, requestBody.IsForm ? "form" : "body");

    private ExpressionNode BuildResponseBody(IrOperation operation, IrResponse response, ExpressionNode responseData)
    {
        if (!_validation.IsEnabled || !Validation.ValidationTypes.IsValidatable(response.Type!))
            return responseData;
        if (response.Type is IrModelReference modelRef)
            return _validation.ValidateNamed($"{modelRef.ModelName}Schema", responseData);
        return _validation.ValidateNamed(
            Validation.ClientResponseSchemas.ResultSchemaName(_namingConverter, operation.MethodName, response.StatusCode),
            responseData);
    }

    private string BuildSchemaImportPath(string modelName, FileOrganization fileOrganization)
    {
        var modelsPath = fileOrganization.ModelsPath ?? "models";
        var modelFileName = _namingConverter.ToCamelCase(_orig.GetValueOrDefault(modelName, modelName));
        return fileOrganization.SeparateModels
            ? $"../../{modelsPath}/{modelFileName}/{modelFileName}.schema"
            : $"../../{modelsPath}/{modelFileName}.schema";
    }

    private static ExpressionNode BuildParameterAccess(ExpressionNode source, string name, bool sourceMayBeUndefined) =>
        source is IdentifierExpressionNode { Identifier.Name: "undefined" }
            ? source
            : new MemberAccessExpressionNode(source, new IdentifierNode(name), useOptionalChaining: sourceMayBeUndefined);

    private static CommentNode? BuildDocumentation(IrOperation operation)
    {
        var doc = operation.Description ?? operation.Summary;
        return string.IsNullOrWhiteSpace(doc) ? null : new CommentNode(doc, CommentKind.JSDoc);
    }

    private static PropertyAssignmentNode SpreadProperty(ExpressionNode value) =>
        new(new IdentifierNode("_"), value, isSpread: true);

    private static ObjectLiteralExpressionNode Object(params (string Name, ExpressionNode Value)[] properties) =>
        new(properties
            .Select(property => property.Name == "_"
                ? SpreadProperty(property.Value)
                : new PropertyAssignmentNode(new IdentifierNode(property.Name), property.Value))
            .ToImmutableList(),
            preferMultilineFormat: true);

    private static ObjectLiteralExpressionNode EmptyObject() => new(ImmutableList<PropertyAssignmentNode>.Empty);

    private static IdentifierExpressionNode Id(string name) => new(new IdentifierNode(name));

    private static MemberAccessExpressionNode Member(ExpressionNode source, string name, bool optional = false) =>
        new(source, new IdentifierNode(name), useOptionalChaining: optional);

    /// <summary>
    /// Builds the Next.js fetch options type
    /// <c>RequestInit &amp; { next?: { revalidate?: number | false; tags?: string[] } }</c> as fully-composed
    /// AST type nodes: an <see cref="IntersectionTypeNode"/> of the <c>RequestInit</c> identifier and an inline
    /// <see cref="TypeLiteralNode"/> for the Next.js fetch augmentation.
    /// </summary>
    /// <returns>The composed options parameter type (no raw type strings).</returns>
    private static TypeNode BuildOptionsType()
    {
        // { revalidate?: number | false; tags?: string[] }
        var nextConfigType = new TypeLiteralNode(ImmutableList.Create(
            new TypeLiteralMember(
                "revalidate",
                new UnionTypeNode(ImmutableList.Create<TypeNode>(
                    Simple("number", isPrimitive: true),
                    new SimpleTypeNode(new IdentifierNode("false"), literalValue: false))),
                isOptional: true),
            new TypeLiteralMember(
                "tags",
                new ArrayTypeNode(Simple("string", isPrimitive: true)),
                isOptional: true)));

        // { next?: { … } }
        var augmentation = new TypeLiteralNode(ImmutableList.Create(
            new TypeLiteralMember("next", nextConfigType, isOptional: true)));

        // RequestInit & { next?: { … } }
        return new IntersectionTypeNode(ImmutableList.Create<TypeNode>(Simple("RequestInit"), augmentation));
    }

    /// <summary>Builds the composed generic type <c>Record&lt;string, string&gt;</c> (no raw type string).</summary>
    /// <returns>A <see cref="GenericTypeNode"/> for <c>Record&lt;string, string&gt;</c>.</returns>
    private static TypeNode RecordStringString() =>
        new GenericTypeNode(
            new IdentifierNode("Record"),
            ImmutableList.Create<TypeNode>(Simple("string", isPrimitive: true), Simple("string", isPrimitive: true)));

    private static SimpleTypeNode Simple(string name, bool isPrimitive = false) => new(new IdentifierNode(name), isPrimitive);

    private static LiteralExpressionNode String(string value) => new(value, LiteralKind.String);

    private static LiteralExpressionNode Number(int value) => new(value, LiteralKind.Number);

    private static LiteralExpressionNode Bool(bool value) => new(value, LiteralKind.Boolean);

    private sealed class ParameterSources
    {
        public ExpressionNode? RouteSource { get; set; }
        public ExpressionNode? QuerySource { get; set; }
        public bool QuerySourceMayBeUndefined { get; set; }
        public ExpressionNode? HeadersSource { get; set; }
        public bool HeadersSourceMayBeUndefined { get; set; }
        public ExpressionNode? BodySource { get; set; }
        public bool BodySourceMayBeUndefined { get; set; }
    }
}
