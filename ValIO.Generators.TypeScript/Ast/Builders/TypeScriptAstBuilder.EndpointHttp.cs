using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// HTTP endpoint: общая подготовка params/Zod и реализация fetch-тела.
/// </summary>
public partial class TypeScriptAstBuilder
{
    private sealed class HttpParamSourcesContext
    {
        public List<StatementNode> LeadingStatements { get; } = new();
        public ExpressionNode? RouteSource { get; set; }
        public ExpressionNode? QuerySource { get; set; }
        public ExpressionNode? BodySource { get; set; }
        public ExpressionNode? HeadersSource { get; set; }
        public bool IsFlat { get; set; }
    }

    private HttpParamSourcesContext PrepareHttpParamSourcesWithOptionalZod(IrOperation operation)
    {
        var ctx = new HttpParamSourcesContext();
        var useZod = _options?.ValidationLibrary == ValidationLibrary.Zod
            && (_options.SerializeWithSchemas || _options.DeserializeWithSchemas);

        var hasRoute = operation.PathParameters.Count > 0;
        var hasQuery = operation.QueryParameters.Count > 0;
        var hasBody = operation.RequestBody != null && (operation.RequestBody.IsJson || operation.RequestBody.IsForm);
        var hasHeaders = operation.HeaderParameters.Count > 0;
        var rb = operation.RequestBody;
        var groupCount = (hasRoute ? 1 : 0) + (hasQuery ? 1 : 0) + (hasBody ? 1 : 0) + (hasHeaders ? 1 : 0);
        var isFlat = groupCount <= 1;
        ctx.IsFlat = isFlat;

        var paramsExpr = new IdentifierExpressionNode(new IdentifierNode("params"));
        ExpressionNode? routeSource = null;
        ExpressionNode? querySource = null;
        ExpressionNode? bodySource = null;
        ExpressionNode? headersSource = null;

        if (isFlat)
        {
            if (hasRoute) routeSource = paramsExpr;
            if (hasQuery) querySource = paramsExpr;
            if (hasBody) bodySource = paramsExpr;
            if (hasHeaders) headersSource = paramsExpr;
        }
        else
        {
            if (hasRoute) routeSource = new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("route"));
            if (hasQuery) querySource = new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("query"));
            if (hasBody) bodySource = new MemberAccessExpressionNode(paramsExpr, new IdentifierNode(rb!.IsJson ? "body" : "form"));
            if (hasHeaders) headersSource = new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("headers"));
        }

        if (useZod && _options!.SerializeWithSchemas)
        {
            var opName = _namingConverter.ToPascalCase(operation.MethodName);
            if (hasRoute)
            {
                var routeInput = isFlat ? paramsExpr : (ExpressionNode)new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("route"));
                var parseCall = CreateZodParseCall($"{opName}RouteParamsSchema", routeInput);
                ctx.LeadingStatements.Add(new VariableDeclarationStatementNode(
                    new IdentifierNode("parsedRoute"),
                    type: null,
                    initializer: parseCall));
                routeSource = new IdentifierExpressionNode(new IdentifierNode("parsedRoute"));
            }
            if (hasQuery)
            {
                var queryInput = isFlat ? paramsExpr : (ExpressionNode)new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("query"));
                var parseCall = CreateZodParseCall($"{opName}QueryParamsSchema", queryInput);
                ctx.LeadingStatements.Add(new VariableDeclarationStatementNode(
                    new IdentifierNode("parsedQuery"),
                    type: null,
                    initializer: parseCall));
                querySource = new IdentifierExpressionNode(new IdentifierNode("parsedQuery"));
            }
            if (hasBody && rb!.Type is IrModelReference bodyModelRef)
            {
                var bodyInput = isFlat ? paramsExpr : (ExpressionNode)new MemberAccessExpressionNode(paramsExpr, new IdentifierNode(rb.IsJson ? "body" : "form"));
                var parseCall = CreateZodParseCall($"{bodyModelRef.ModelName}Schema", bodyInput);
                ctx.LeadingStatements.Add(new VariableDeclarationStatementNode(
                    new IdentifierNode("parsedBody"),
                    type: null,
                    initializer: parseCall));
                bodySource = new IdentifierExpressionNode(new IdentifierNode("parsedBody"));
            }
            ctx.LeadingStatements.Add(CreateBlankLineStatement());
        }

        ctx.RouteSource = routeSource;
        ctx.QuerySource = querySource;
        ctx.BodySource = bodySource;
        ctx.HeadersSource = headersSource;
        return ctx;
    }

    /// <summary>
    /// Добавляет к path query-string из <paramref name="querySource"/> по именам параметров из IR.
    /// </summary>
    private static ExpressionNode BuildUrlWithQueryStringExpression(
        IrOperation operation,
        ExpressionNode pathUrlExpr,
        ExpressionNode? querySource)
    {
        if (operation.QueryParameters.Count == 0 || querySource == null)
            return pathUrlExpr;

        var parts = new List<TemplateLiteralPart>
        {
            new(pathUrlExpr),
            new("?")
        };

        for (var i = 0; i < operation.QueryParameters.Count; i++)
        {
            var qp = operation.QueryParameters[i];
            if (i > 0)
                parts.Add(new TemplateLiteralPart("&"));
            parts.Add(new TemplateLiteralPart($"{qp.Name}="));
            var member = new MemberAccessExpressionNode(querySource, new IdentifierNode(qp.Name));
            var asString = new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("String")),
                new[] { member }.ToImmutableList());
            var encoded = new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("encodeURIComponent")),
                new[] { asString }.ToImmutableList());
            parts.Add(new TemplateLiteralPart(encoded));
        }

        return new TemplateLiteralExpressionNode(parts.ToImmutableList());
    }

    private static ExpressionNode? BuildFetchHeadersObject(
        ExpressionNode? headersSource,
        bool hasJsonBody)
    {
        if (!hasJsonBody && headersSource == null)
            return null;

        if (hasJsonBody && headersSource == null)
        {
            return new ObjectLiteralExpressionNode(new[]
            {
                new PropertyAssignmentNode(
                    new IdentifierNode("'Content-Type'"),
                    new LiteralExpressionNode("application/json", LiteralKind.String))
            }.ToImmutableList());
        }

        if (!hasJsonBody && headersSource != null)
            return headersSource;

        var placeholder = new IdentifierNode("_");
        return new ObjectLiteralExpressionNode(new[]
        {
            new PropertyAssignmentNode(placeholder, headersSource!, isSpread: true),
            new PropertyAssignmentNode(
                new IdentifierNode("'Content-Type'"),
                new LiteralExpressionNode("application/json", LiteralKind.String))
        }.ToImmutableList(), preferMultilineFormat: true);
    }

    private BlockNode BuildFetchFunctionBody(IrOperation operation)
    {
        var statements = new List<StatementNode>();
        var ctx = PrepareHttpParamSourcesWithOptionalZod(operation);
        statements.AddRange(ctx.LeadingStatements);

        var axiosBuilder = new Axios.Ast.Builders.AxiosAstBuilder(_namingConverter, BuildType);
        var pathUrlExpr = axiosBuilder.BuildUrlWithSource(operation, ctx.RouteSource);
        var requestUrlExpr = BuildUrlWithQueryStringExpression(operation, pathUrlExpr, ctx.QuerySource);

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("requestUrl"),
            type: null,
            initializer: requestUrlExpr));
        statements.Add(CreateBlankLineStatement());

        var methodUpper = operation.HttpMethod.ToUpperInvariant();
        var methodLiteral = new LiteralExpressionNode(methodUpper, LiteralKind.String);

        var hasJsonBody = operation.RequestBody != null && operation.RequestBody.IsJson && ctx.BodySource != null;
        var headersExpr = BuildFetchHeadersObject(ctx.HeadersSource, hasJsonBody);

        var initProps = new List<PropertyAssignmentNode>
        {
            new(new IdentifierNode("method"), methodLiteral)
        };

        if (headersExpr != null)
            initProps.Add(new PropertyAssignmentNode(new IdentifierNode("headers"), headersExpr));

        if (operation.RequestBody != null && operation.RequestBody.IsJson && ctx.BodySource != null)
        {
            var jsonStringify = new CallExpressionNode(
                new MemberAccessExpressionNode(
                    new IdentifierExpressionNode(new IdentifierNode("JSON")),
                    new IdentifierNode("stringify")),
                new[] { ctx.BodySource }.ToImmutableList());
            initProps.Add(new PropertyAssignmentNode(new IdentifierNode("body"), jsonStringify));
        }
        else if (operation.RequestBody != null && operation.RequestBody.IsForm && ctx.BodySource != null)
        {
            initProps.Add(new PropertyAssignmentNode(new IdentifierNode("body"), ctx.BodySource));
        }

        var signalRef = new IdentifierExpressionNode(new IdentifierNode("signal"));
        var signalSpread = new ConditionalExpressionNode(
            signalRef,
            new ObjectLiteralExpressionNode(new[]
            {
                new PropertyAssignmentNode(new IdentifierNode("signal"), signalRef)
            }.ToImmutableList()),
            new ObjectLiteralExpressionNode(ImmutableList<PropertyAssignmentNode>.Empty));

        var baseInit = new ObjectLiteralExpressionNode(initProps.ToImmutableList(), type: null, metadata: null, preferMultilineFormat: true);
        var optionsRef = new IdentifierExpressionNode(new IdentifierNode("options"));
        var emptyObj = new ObjectLiteralExpressionNode(ImmutableList<PropertyAssignmentNode>.Empty);
        var optionsSpread = new BinaryExpressionNode(optionsRef, BinaryOperator.NullishCoalescing, emptyObj);
        var placeholder = new IdentifierNode("_");
        var mergedInit = new ObjectLiteralExpressionNode(new[]
        {
            new PropertyAssignmentNode(placeholder, new IdentifierExpressionNode(new IdentifierNode("baseInit")), isSpread: true),
            new PropertyAssignmentNode(placeholder, optionsSpread, isSpread: true),
            new PropertyAssignmentNode(placeholder, signalSpread, isSpread: true)
        }.ToImmutableList(), type: null, metadata: null, preferMultilineFormat: true);

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("baseInit"),
            type: new SimpleTypeNode(new IdentifierNode("RequestInit"), isPrimitive: false),
            initializer: baseInit));

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("init"),
            type: new SimpleTypeNode(new IdentifierNode("RequestInit"), isPrimitive: false),
            initializer: mergedInit));

        var fetchName = GetFetchImplementationName();
        var fetchCall = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode(fetchName)),
            new[]
            {
                new IdentifierExpressionNode(new IdentifierNode("requestUrl")),
                new IdentifierExpressionNode(new IdentifierNode("init"))
            }.ToImmutableList());

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("response"),
            type: null,
            initializer: new AwaitExpressionNode(fetchCall)));
        statements.Add(CreateBlankLineStatement());

        var jsonCall = new CallExpressionNode(
            new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("response")),
                new IdentifierNode("json")),
            ImmutableList<ExpressionNode>.Empty);

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("responseData"),
            type: null,
            initializer: new AwaitExpressionNode(jsonCall)));
        statements.Add(CreateBlankLineStatement());

        var useZod = _options?.ValidationLibrary == ValidationLibrary.Zod
            && (_options.SerializeWithSchemas || _options.DeserializeWithSchemas);
        var useResultType = operation.Responses.Any(r => r.Type != null);
        var operationReturnType = useResultType
            ? new SimpleTypeNode(new IdentifierNode("unknown"), isPrimitive: false)
            : ExtractReturnType(operation.Responses);

        if (useResultType)
        {
            var responseStatusRef = new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("response")),
                new IdentifierNode("status"));
            var responseDataRef = new IdentifierExpressionNode(new IdentifierNode("responseData"));
            var responsesWithType = operation.Responses
                .Where(r => r.Type is IrModelReference && int.TryParse(r.StatusCode, out _))
                .ToList();

            foreach (var resp in responsesWithType)
            {
                var statusCode = int.Parse(resp.StatusCode);
                var isSuccessful = statusCode >= 200 && statusCode < 300;
                var modelRef = (IrModelReference)resp.Type!;
                var schemaName = $"{modelRef.ModelName}Schema";
                var parseCall = useZod && _options!.DeserializeWithSchemas
                    ? CreateZodParseCall(schemaName, responseDataRef)
                    : (ExpressionNode)responseDataRef;

                var returnObj = new ObjectLiteralExpressionNode(new[]
                {
                    new PropertyAssignmentNode(new IdentifierNode("status"),
                        new LiteralExpressionNode(statusCode, LiteralKind.Number)),
                    new PropertyAssignmentNode(new IdentifierNode("body"), parseCall),
                    new PropertyAssignmentNode(new IdentifierNode("isSuccessful"),
                        new LiteralExpressionNode(isSuccessful, LiteralKind.Boolean)),
                    new PropertyAssignmentNode(new IdentifierNode("isFailed"),
                        new LiteralExpressionNode(!isSuccessful, LiteralKind.Boolean))
                }.ToImmutableList());

                var statusLiteral = new LiteralExpressionNode(statusCode, LiteralKind.Number);
                var condition = new BinaryExpressionNode(responseStatusRef, BinaryOperator.Equal, statusLiteral);

                statements.Add(new IfStatementNode(
                    condition,
                    new BlockNode(new[] { new ReturnStatementNode(returnObj) }.ToImmutableList()),
                    null));
            }

            statements.Add(new ReturnStatementNode(
                new ObjectLiteralExpressionNode(new[]
                {
                    new PropertyAssignmentNode(new IdentifierNode("status"), responseStatusRef),
                    new PropertyAssignmentNode(new IdentifierNode("body"), responseDataRef),
                    new PropertyAssignmentNode(new IdentifierNode("isSuccessful"),
                        new BinaryExpressionNode(
                            new BinaryExpressionNode(responseStatusRef, BinaryOperator.GreaterThanOrEqual, new LiteralExpressionNode(200, LiteralKind.Number)),
                            BinaryOperator.LogicalAnd,
                            new BinaryExpressionNode(responseStatusRef, BinaryOperator.LessThan, new LiteralExpressionNode(300, LiteralKind.Number)))),
                    new PropertyAssignmentNode(new IdentifierNode("isFailed"),
                        new UnaryExpressionNode(UnaryOperator.LogicalNot,
                            new BinaryExpressionNode(
                                new BinaryExpressionNode(responseStatusRef, BinaryOperator.GreaterThanOrEqual, new LiteralExpressionNode(200, LiteralKind.Number)),
                                BinaryOperator.LogicalAnd,
                                new BinaryExpressionNode(responseStatusRef, BinaryOperator.LessThan, new LiteralExpressionNode(300, LiteralKind.Number)))))
                }.ToImmutableList())));
        }
        else if (operationReturnType is not SimpleTypeNode { Name.Name: "void" })
        {
            if (useZod && _options!.DeserializeWithSchemas)
            {
                var successResponse = operation.Responses.FirstOrDefault(r =>
                    int.TryParse(r.StatusCode, out var code) && code >= 200 && code < 300);
                if (successResponse?.Type is IrModelReference responseModelRef)
                {
                    var parseCall = CreateZodParseCall($"{responseModelRef.ModelName}Schema",
                        new IdentifierExpressionNode(new IdentifierNode("responseData")));
                    statements.Add(new VariableDeclarationStatementNode(
                        new IdentifierNode("parsedResponse"),
                        type: null,
                        initializer: parseCall));
                    statements.Add(new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("parsedResponse"))));
                }
                else
                {
                    statements.Add(new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("responseData"))));
                }
            }
            else
            {
                statements.Add(new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("responseData"))));
            }
        }

        return new BlockNode(statements.ToImmutableList());
    }

    private string GetFetchImplementationName()
    {
        var fetchOpts = _options?.Fetch;
        if (!string.IsNullOrWhiteSpace(fetchOpts?.CustomFetcherPath) && !string.IsNullOrWhiteSpace(fetchOpts?.CustomFetcherName))
            return fetchOpts.CustomFetcherName;
        return "fetch";
    }
}
