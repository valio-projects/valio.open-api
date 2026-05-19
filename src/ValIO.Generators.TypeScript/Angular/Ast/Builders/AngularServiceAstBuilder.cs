using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.Models;
using ValIO.Generators.TypeScript.Angular.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Angular.Ast.Builders;

/// <summary>
/// Builds a single <c>@Injectable({ providedIn: 'root' })</c> service per
/// <see cref="IrClient"/>, emitting one method per <see cref="IrOperation"/>.
/// Each method returns <c>Observable&lt;{Op}Result&gt;</c> and delegates to
/// <c>this.http.{verb}&lt;T&gt;(url, { params, headers, body? })</c>.
/// </summary>
public sealed class AngularServiceAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptParamsAstBuilder _paramsBuilder;
    private readonly TypeScriptResultsAstBuilder _resultsBuilder;
    private readonly TypeScriptGenerationOptions? _options;

    public AngularServiceAstBuilder(INamingConverter namingConverter, TypeScriptGenerationOptions? options)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _options = options;
        var typeBuilder = new TypeScriptAstBuilder(namingConverter, options);
        _paramsBuilder = new TypeScriptParamsAstBuilder(namingConverter, typeBuilder.BuildType);
        _resultsBuilder = new TypeScriptResultsAstBuilder(namingConverter, typeBuilder.BuildType);
    }

    /// <summary>
    /// Service class name: <c>{Pascal(IrClient.Name)}Service</c>.
    /// </summary>
    public string ServiceClassName(string clientName) =>
        $"{_namingConverter.ToPascalCase(clientName)}Service";

    /// <summary>
    /// Service file basename (camelCase + "Service"), e.g. "petService".
    /// </summary>
    public string ServiceFileName(string clientName) =>
        $"{_namingConverter.ToCamelCase(clientName)}Service";

    /// <summary>
    /// Builds a complete <see cref="SourceFileNode"/> for one IrClient,
    /// containing the Angular @Injectable service declaration.
    /// </summary>
    public SourceFileNode BuildServiceSourceFile(
        IrClient client,
        FileOrganization? fileOrganization,
        TypeScriptGenerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var imports = BuildImports(client, fileOrganization);
        var serviceClass = BuildServiceClass(client);

        return new SourceFileNode(
            imports: imports,
            declarations: ImmutableList.Create<DeclarationNode>(serviceClass));
    }

    private AngularServiceClassNode BuildServiceClass(IrClient client)
    {
        var className = ServiceClassName(client.Name);

        // @Injectable({ providedIn: "root" })
        var providedInProperty = new PropertyAssignmentNode(
            new IdentifierNode("providedIn"),
            new LiteralExpressionNode("root", LiteralKind.String));
        var injectableArgs = ImmutableList.Create<ExpressionNode>(
            new ObjectLiteralExpressionNode(
                ImmutableList.Create(providedInProperty),
                preferMultilineFormat: false));
        var injectableAttribute = new AttributeNode(
            new IdentifierNode("Injectable"),
            injectableArgs);

        // constructor(private readonly http: HttpClient) {}
        var httpParameter = new ParameterDeclarationNode(
            name: new IdentifierNode("http"),
            type: new SimpleTypeNode(new IdentifierNode("HttpClient"), isPrimitive: false),
            isRequired: true,
            metadata: new Dictionary<string, object> { ["accessModifier"] = "private readonly" });

        var methods = client.Operations
            .Select(BuildOperationMethod)
            .ToImmutableList();

        return new AngularServiceClassNode(
            name: new IdentifierNode(className),
            attributes: ImmutableList.Create(injectableAttribute),
            constructorParameters: ImmutableList.Create(httpParameter),
            methods: methods,
            isExported: true,
            documentation: new CommentNode(
                $"Angular RxJS HttpClient service for {client.Name} operations.",
                CommentKind.JSDoc));
    }

    private MethodDeclarationNode BuildOperationMethod(IrOperation operation)
    {
        var hasParameters = _paramsBuilder.HasParameters(operation);
        var hasResultType = _resultsBuilder.HasResponsesWithBody(operation);
        var resultTypeName = _resultsBuilder.ResultTypeName(operation.MethodName);

        // Return type: Observable<{Op}Result> when there is a body, otherwise Observable<void>.
        var resultTypeNode = hasResultType
            ? new SimpleTypeNode(new IdentifierNode(resultTypeName), isPrimitive: false)
            : new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true);
        var returnType = new GenericTypeNode(
            new IdentifierNode("Observable"),
            ImmutableList.Create<TypeNode>(resultTypeNode));

        // Parameters.
        var parameters = ImmutableList.CreateBuilder<ParameterDeclarationNode>();
        if (hasParameters)
        {
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("params"),
                type: new SimpleTypeNode(
                    new IdentifierNode(_paramsBuilder.ParamsInterfaceName(operation.MethodName)),
                    isPrimitive: false),
                isRequired: true));
        }

        return new MethodDeclarationNode(
            name: new IdentifierNode(operation.MethodName),
            returnType: returnType,
            parameters: parameters.ToImmutable(),
            body: BuildMethodBody(operation, hasResultType, resultTypeName),
            isAsync: false,
            accessModifier: AccessModifier.Public,
            documentation: BuildDocumentation(operation));
    }

    private BlockNode BuildMethodBody(IrOperation operation, bool hasResultType, string resultTypeName)
    {
        var statements = new List<StatementNode>();
        var sources = BuildParameterSources(operation);

        // const url = `/path/${params.id}`;
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("url"),
            type: null,
            initializer: BuildUrlExpression(operation, sources)));

        // Build HttpParams + HttpHeaders only if relevant.
        var hasOptionsObject = false;
        var optionsProperties = new List<PropertyAssignmentNode>();

        if (operation.QueryParameters.Count > 0 && sources.QuerySource != null)
        {
            statements.Add(new BlankLineStatementNode());
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("queryParams"),
                type: null,
                initializer: BuildHttpParamsChain(operation, sources)));
            optionsProperties.Add(new PropertyAssignmentNode(
                new IdentifierNode("params"),
                Id("queryParams")));
            hasOptionsObject = true;
        }

        if (operation.HeaderParameters.Count > 0 && sources.HeadersSource != null)
        {
            statements.Add(new BlankLineStatementNode());
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("headers"),
                type: null,
                initializer: BuildHttpHeadersChain(operation, sources)));
            optionsProperties.Add(new PropertyAssignmentNode(
                new IdentifierNode("headers"),
                Id("headers")));
            hasOptionsObject = true;
        }

        // Build options object — only when there is at least one option.
        var optionsObject = hasOptionsObject
            ? (ExpressionNode)new ObjectLiteralExpressionNode(
                optionsProperties.ToImmutableList(),
                preferMultilineFormat: optionsProperties.Count > 1)
            : null;

        if (statements.Count > 0)
            statements.Add(new BlankLineStatementNode());

        // return this.http.{verb}<T>(url, body?, options?);
        statements.Add(new ReturnStatementNode(
            BuildHttpCall(operation, sources, hasResultType, resultTypeName, optionsObject)));

        return new BlockNode(statements.ToImmutableList());
    }

    private ExpressionNode BuildHttpCall(
        IrOperation operation,
        ParameterSources sources,
        bool hasResultType,
        string resultTypeName,
        ExpressionNode? optionsObject)
    {
        var verb = operation.HttpMethod.ToLowerInvariant();
        // HttpClient.get/delete/head signatures: (url, options?). For these verbs,
        // body — if any — would be ignored on the client side and so omitted.
        var verbHasBody = verb is "post" or "put" or "patch";
        var hasBody = verbHasBody && operation.RequestBody != null && sources.BodySource != null;

        var arguments = new List<ExpressionNode> { Id("url") };

        if (hasBody)
        {
            // For binary body (Blob / Uint8Array), Angular HttpClient accepts
            // many ad-hoc body shapes; the body interface from our params
            // doesn't always line up with the union, so cast through unknown.
            var bodyExpression = operation.RequestBody!.IsJson
                ? sources.BodySource!
                : (ExpressionNode)new CastExpressionNode(
                    new SimpleTypeNode(new IdentifierNode("unknown"), isPrimitive: true),
                    sources.BodySource!,
                    useAsOperator: true);
            arguments.Add(bodyExpression);
        }

        if (optionsObject != null)
            arguments.Add(optionsObject);

        // Generic type argument: e.g. `this.http.get<GetUserResult>(...)`.
        // Skip when the body is void — generic isn't useful and stops tsc
        // from inferring `Observable<Object>`.
        var genericTypeArg = hasResultType
            ? (TypeNode)new SimpleTypeNode(new IdentifierNode(resultTypeName), isPrimitive: false)
            : new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true);

        return new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new MemberAccessExpressionNode(
                    @object: new IdentifierExpressionNode(new IdentifierNode("this")),
                    member: new IdentifierNode("http")),
                member: new IdentifierNode(verb)),
            arguments: arguments.ToImmutableList(),
            genericTypeArgument: genericTypeArg);
    }

    private ExpressionNode BuildHttpParamsChain(IrOperation operation, ParameterSources sources)
    {
        // Start with `new HttpParams()` then chain `.set('a', String(params.query.a))`
        // for each query parameter. Conditional sets for optional params via
        // `someOptional !== undefined ? base.set(...) : base` — but to keep the
        // chain readable we always coerce the value.
        ExpressionNode chain = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("HttpParams")),
            ImmutableList<ExpressionNode>.Empty,
            isNewExpression: true);

        foreach (var parameter in operation.QueryParameters)
        {
            var access = BuildParameterAccess(sources.QuerySource!, parameter.Name, sources.QuerySourceMayBeUndefined);
            var coerced = new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("String")),
                ImmutableList.Create<ExpressionNode>(access));

            var setCall = new CallExpressionNode(
                new MemberAccessExpressionNode(chain, new IdentifierNode("set")),
                ImmutableList.Create<ExpressionNode>(
                    new LiteralExpressionNode(parameter.Name, LiteralKind.String),
                    coerced));

            if (!parameter.IsRequired)
            {
                // For optional params, skip `set` when the value is undefined so
                // we don't send literal "undefined" strings on the wire.
                chain = new ConditionalExpressionNode(
                    new BinaryExpressionNode(
                        access,
                        BinaryOperator.NotEqual,
                        new IdentifierExpressionNode(new IdentifierNode("undefined"))),
                    setCall,
                    chain);
            }
            else
            {
                chain = setCall;
            }
        }

        return chain;
    }

    private ExpressionNode BuildHttpHeadersChain(IrOperation operation, ParameterSources sources)
    {
        ExpressionNode chain = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("HttpHeaders")),
            ImmutableList<ExpressionNode>.Empty,
            isNewExpression: true);

        foreach (var parameter in operation.HeaderParameters)
        {
            var access = BuildParameterAccess(sources.HeadersSource!, parameter.Name, sources.HeadersSourceMayBeUndefined);
            var coerced = new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("String")),
                ImmutableList.Create<ExpressionNode>(access));

            var setCall = new CallExpressionNode(
                new MemberAccessExpressionNode(chain, new IdentifierNode("set")),
                ImmutableList.Create<ExpressionNode>(
                    new LiteralExpressionNode(parameter.Name, LiteralKind.String),
                    coerced));

            if (!parameter.IsRequired)
            {
                chain = new ConditionalExpressionNode(
                    new BinaryExpressionNode(
                        access,
                        BinaryOperator.NotEqual,
                        new IdentifierExpressionNode(new IdentifierNode("undefined"))),
                    setCall,
                    chain);
            }
            else
            {
                chain = setCall;
            }
        }

        return chain;
    }

    private static ExpressionNode BuildUrlExpression(IrOperation operation, ParameterSources sources)
    {
        if (operation.PathParameters.Count == 0 || sources.RouteSource == null)
            return new LiteralExpressionNode(operation.Path, LiteralKind.String);

        var parts = new List<TemplateLiteralPart>();
        var cursor = 0;

        foreach (var parameter in operation.PathParameters)
        {
            var token = $"{{{parameter.Name}}}";
            var index = operation.Path.IndexOf(token, cursor, StringComparison.Ordinal);
            if (index < 0)
                continue;

            if (index > cursor)
                parts.Add(new TemplateLiteralPart(operation.Path[cursor..index]));
            parts.Add(new TemplateLiteralPart(BuildParameterAccess(sources.RouteSource, parameter.Name, sourceMayBeUndefined: false)));
            cursor = index + token.Length;
        }

        if (cursor < operation.Path.Length)
            parts.Add(new TemplateLiteralPart(operation.Path[cursor..]));

        return parts.Count == 0
            ? new LiteralExpressionNode(operation.Path, LiteralKind.String)
            : new TemplateLiteralExpressionNode(parts.ToImmutableList());
    }

    private static ParameterSources BuildParameterSources(IrOperation operation)
    {
        var hasRoute = operation.PathParameters.Count > 0;
        var hasQuery = operation.QueryParameters.Count > 0;
        var hasHeaders = operation.HeaderParameters.Count > 0;
        var hasBody = operation.RequestBody != null;
        var groupCount = (hasRoute ? 1 : 0) + (hasQuery ? 1 : 0) + (hasHeaders ? 1 : 0) + (hasBody ? 1 : 0);
        var isFlat = groupCount <= 1;
        var paramsExpr = (ExpressionNode)new IdentifierExpressionNode(new IdentifierNode("params"));

        return new ParameterSources
        {
            RouteSource = hasRoute ? (isFlat ? paramsExpr : Member(paramsExpr, "route")) : null,
            QuerySource = hasQuery ? (isFlat ? paramsExpr : Member(paramsExpr, "query")) : null,
            QuerySourceMayBeUndefined = hasQuery && !isFlat && operation.QueryParameters.All(p => !p.IsRequired),
            HeadersSource = hasHeaders ? (isFlat ? paramsExpr : Member(paramsExpr, "headers")) : null,
            HeadersSourceMayBeUndefined = hasHeaders && !isFlat,
            BodySource = hasBody ? Member(paramsExpr, operation.RequestBody!.IsForm ? "form" : "body") : null,
            BodySourceMayBeUndefined = hasBody && operation.RequestBody?.IsRequired == false,
        };
    }

    private static ExpressionNode BuildParameterAccess(ExpressionNode source, string name, bool sourceMayBeUndefined) =>
        new MemberAccessExpressionNode(source, new IdentifierNode(name), useOptionalChaining: sourceMayBeUndefined);

    private static MemberAccessExpressionNode Member(ExpressionNode source, string name) =>
        new(source, new IdentifierNode(name));

    private static IdentifierExpressionNode Id(string name) =>
        new(new IdentifierNode(name));

    private IReadOnlyList<ImportNode> BuildImports(IrClient client, FileOrganization? fileOrganization)
    {
        var imports = new List<ImportNode>
        {
            new(
                items: ImmutableList.Create(new IdentifierNode("Injectable")),
                from: "@angular/core",
                kind: ImportKind.Named),
        };

        // HttpClient + co. Only include HttpHeaders/HttpParams when used.
        var commonHttpItems = new List<IdentifierNode> { new("HttpClient") };
        var anyQuery = client.Operations.Any(op => op.QueryParameters.Count > 0);
        var anyHeaders = client.Operations.Any(op => op.HeaderParameters.Count > 0);
        if (anyHeaders) commonHttpItems.Add(new IdentifierNode("HttpHeaders"));
        if (anyQuery) commonHttpItems.Add(new IdentifierNode("HttpParams"));
        imports.Add(new ImportNode(
            items: commonHttpItems.ToImmutableList(),
            from: "@angular/common/http",
            kind: ImportKind.Named));

        imports.Add(new ImportNode(
            items: ImmutableList.Create(new IdentifierNode("Observable")),
            from: "rxjs",
            kind: ImportKind.Named));

        // Type imports for Params/Result interfaces per operation.
        var clientsPath = fileOrganization?.ClientsPath ?? "api";
        foreach (var operation in client.Operations)
        {
            var operationFileName = _namingConverter.ToCamelCase(operation.MethodName);
            if (_paramsBuilder.HasParameters(operation))
            {
                imports.Add(new ImportNode(
                    items: ImmutableList.Create(new IdentifierNode(_paramsBuilder.ParamsInterfaceName(operation.MethodName))),
                    from: $"./{operationFileName}/{operationFileName}.params",
                    kind: ImportKind.Named,
                    isTypeOnly: true));
            }
            if (_resultsBuilder.HasResponsesWithBody(operation))
            {
                imports.Add(new ImportNode(
                    items: ImmutableList.Create(new IdentifierNode(_resultsBuilder.ResultTypeName(operation.MethodName))),
                    from: $"./{operationFileName}/{operationFileName}.results",
                    kind: ImportKind.Named,
                    isTypeOnly: true));
            }
        }

        return imports;
    }

    private static CommentNode? BuildDocumentation(IrOperation operation)
    {
        var doc = operation.Description ?? operation.Summary;
        return string.IsNullOrWhiteSpace(doc)
            ? null
            : new CommentNode(doc, CommentKind.JSDoc);
    }

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
