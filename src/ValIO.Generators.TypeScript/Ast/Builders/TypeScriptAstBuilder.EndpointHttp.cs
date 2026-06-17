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
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Validation;

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

    /// <summary>
    /// Доступ к телу запроса внутри объекта <c>params</c>: тело всегда вложено — в
    /// <c>params.form</c> (multipart/url-encoded) либо <c>params.body</c> (JSON и любой иной
    /// content-type). Согласовано с <c>TypeScriptParamsAstBuilder</c> и bespoke-транспортами.
    /// </summary>
    private static MemberAccessExpressionNode BodyPropertyAccess(IrRequestBody requestBody, ExpressionNode paramsExpr)
        => new(paramsExpr, new IdentifierNode(requestBody.IsForm ? "form" : "body"));

    private HttpParamSourcesContext PrepareHttpParamSourcesWithOptionalZod(IrOperation operation)
    {
        var ctx = new HttpParamSourcesContext();

        var hasRoute = operation.PathParameters.Count > 0;
        var hasQuery = operation.QueryParameters.Count > 0;
        var rb = operation.RequestBody;
        // Любое тело запроса (JSON, multipart/url-encoded form, или иной content-type —
        // напр. application/octet-stream) образует одну группу параметров — так же, как
        // в TypeScriptParamsAstBuilder, иначе groupCount/flat рассинхронизируются с params-интерфейсом.
        var hasBody = rb != null;
        var hasHeaders = operation.HeaderParameters.Count > 0;
        var groupCount = (hasRoute ? 1 : 0) + (hasQuery ? 1 : 0) + (hasBody ? 1 : 0) + (hasHeaders ? 1 : 0);
        var isFlat = groupCount <= 1;
        ctx.IsFlat = isFlat;

        var paramsExpr = new IdentifierExpressionNode(new IdentifierNode("params"));

        // Route/query/headers «расплющиваются» в сам params, когда являются единственной группой
        // (плоский интерфейс кладёт их поля прямо в params). ТЕЛО запроса, напротив, ВСЕГДА лежит
        // во вложенном свойстве params.body / params.form (см. BuildFlatParamsInterface) — и в flat,
        // и в aggregate. Поэтому body-источник не зависит от isFlat.
        ExpressionNode? routeSource = hasRoute
            ? (isFlat ? paramsExpr : new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("route")))
            : null;
        ExpressionNode? querySource = hasQuery
            ? (isFlat ? paramsExpr : new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("query")))
            : null;
        ExpressionNode? headersSource = hasHeaders
            ? (isFlat ? paramsExpr : new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("headers")))
            : null;
        ExpressionNode? bodySource = hasBody ? BodyPropertyAccess(rb!, paramsExpr) : null;

        if (_validation.IsEnabled)
        {
            var opName = _namingConverter.ToPascalCase(operation.MethodName);
            var before = ctx.LeadingStatements.Count;

            // Route/query параметры валидируются по импортируемой именованной схеме
            // ({op}.params.schema.ts) — единообразно для всех библиотек.
            if (hasRoute)
            {
                var routeInput = isFlat ? paramsExpr : (ExpressionNode)new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("route"));
                var parseCall = _validation.ValidateNamed($"{opName}RouteParamsSchema", routeInput);
                ctx.LeadingStatements.Add(new VariableDeclarationStatementNode(
                    new IdentifierNode("parsedRoute"),
                    type: null,
                    initializer: parseCall));
                routeSource = new IdentifierExpressionNode(new IdentifierNode("parsedRoute"));
            }
            if (hasQuery)
            {
                var queryInput = isFlat ? paramsExpr : (ExpressionNode)new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("query"));
                var parseCall = _validation.ValidateNamed($"{opName}QueryParamsSchema", queryInput);
                ctx.LeadingStatements.Add(new VariableDeclarationStatementNode(
                    new IdentifierNode("parsedQuery"),
                    type: null,
                    initializer: parseCall));
                querySource = new IdentifierExpressionNode(new IdentifierNode("parsedQuery"));
            }
            // Тело запроса валидируется схемой, построенной из типа тела (модель/union/...) выбранной библиотекой.
            if (hasBody && ValidationTypes.IsValidatable(rb!.Type))
            {
                var bodyInput = BodyPropertyAccess(rb, paramsExpr);
                var parseCall = _validation.Validate(rb.Type, bodyInput);
                ctx.LeadingStatements.Add(new VariableDeclarationStatementNode(
                    new IdentifierNode("parsedBody"),
                    type: null,
                    initializer: parseCall));
                bodySource = new IdentifierExpressionNode(new IdentifierNode("parsedBody"));
            }

            if (ctx.LeadingStatements.Count > before)
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

        // Если все query параметры optional — querySource (params.query) сам может
        // быть undefined, поэтому к доступу используем optional chaining.
        var sourceMayBeUndefined = operation.QueryParameters.All(p => !p.IsRequired);

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
            var member = new MemberAccessExpressionNode(
                querySource,
                new IdentifierNode(qp.Name),
                useOptionalChaining: sourceMayBeUndefined);
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
            return CastToHeadersInit(headersSource);

        var placeholder = new IdentifierNode("_");
        return new ObjectLiteralExpressionNode(new[]
        {
            new PropertyAssignmentNode(placeholder, CastToHeadersInit(headersSource!), isSpread: true),
            new PropertyAssignmentNode(
                new IdentifierNode("'Content-Type'"),
                new LiteralExpressionNode("application/json", LiteralKind.String))
        }.ToImmutableList(), preferMultilineFormat: true);
    }

    private static CastExpressionNode CastToHeadersInit(ExpressionNode source)
    {
        // {Op}HeaderParams интерфейс не имеет index-signature, а HeadersInit
        // требует Record<string, string>. Делаем `as HeadersInit`-cast.
        return new CastExpressionNode(
            new SimpleTypeNode(new IdentifierNode("HeadersInit"), isPrimitive: false),
            source,
            useAsOperator: true);
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
        else if (operation.RequestBody != null && ctx.BodySource != null)
        {
            // Не-JSON тело: form-data / url-encoded (params.form) или иной content-type,
            // напр. application/octet-stream (params.body). Типизированный источник (узкий
            // интерфейс) не совпадает с BodyInit — кастуем через unknown, runtime строит
            // FormData/Blob/stream сам.
            var bodyCast = new CastExpressionNode(
                new SimpleTypeNode(new IdentifierNode("BodyInit"), isPrimitive: false),
                new CastExpressionNode(
                    new SimpleTypeNode(new IdentifierNode("unknown"), isPrimitive: true),
                    ctx.BodySource,
                    useAsOperator: true),
                useAsOperator: true);
            initProps.Add(new PropertyAssignmentNode(new IdentifierNode("body"), bodyCast));
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
                .Where(r => r.Type != null && int.TryParse(r.StatusCode, out _))
                .ToList();

            foreach (var resp in responsesWithType)
            {
                var statusCode = int.Parse(resp.StatusCode);
                var isSuccessful = statusCode >= 200 && statusCode < 300;
                var parseCall = BuildStatusResultBody(operation, resp, responseDataRef);

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
            if (_validation.IsEnabled)
            {
                var successResponse = operation.Responses.FirstOrDefault(r =>
                    int.TryParse(r.StatusCode, out var code) && code >= 200 && code < 300);
                if (successResponse?.Type is IrModelReference responseModelRef)
                {
                    var parseCall = CreateParseCall($"{responseModelRef.ModelName}Schema",
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

    private string GetFetchImplementationName() => "fetch";
}
