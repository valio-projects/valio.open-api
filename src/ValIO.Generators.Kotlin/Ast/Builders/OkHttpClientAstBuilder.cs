using System.Text;
using ValIO.Core.Ast.Builders;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Kotlin.Ast.Nodes;

namespace ValIO.Generators.Kotlin.Ast.Builders;

/// <summary>
/// Builds an OkHttp + coroutines service class for one <see cref="IrClient"/>.
/// </summary>
/// <param name="namingConverter">The source identifier converter.</param>
/// <param name="typeMapper">The Kotlin type mapper.</param>
public sealed class OkHttpClientAstBuilder(INamingConverter namingConverter, KotlinIrTypeMapper typeMapper)
{

    /// <summary>Builds a caller-owned OkHttp service with coroutine-aware native request cancellation.</summary>
    /// <param name="client">The operation group.</param>
    /// <param name="apiPackage">The client declaration package.</param>
    /// <param name="modelsPackage">The generated model package.</param>
    /// <param name="knownModelNames">The finalized model and enum names.</param>
    /// <returns>The service and its cancellation adapter.</returns>
    public KotlinFileNode BuildFile(
        IrClient client,
        string apiPackage,
        string modelsPackage,
        IReadOnlySet<string> knownModelNames)
    {
        var className = ToClassName(client.Name);
        var importScan = new KotlinTypeImportScanner.Result();

        // Primary-constructor properties.
        var ctorProps = new List<KotlinPropertyNode>
        {
            new KotlinPropertyNode(
                new IdentifierNode("client"),
                new SimpleTypeNode(new IdentifierNode("OkHttpClient"), isPrimitive: true),
                visibility: "private",
                isReadOnly: true,
                isConstructorParameter: true),
            new KotlinPropertyNode(
                new IdentifierNode("baseUrl"),
                new SimpleTypeNode(new IdentifierNode("HttpUrl"), isPrimitive: true),
                visibility: "private",
                isReadOnly: true,
                isConstructorParameter: true),
            new KotlinPropertyNode(
                new IdentifierNode("json"),
                new SimpleTypeNode(new IdentifierNode("Json"), isPrimitive: true),
                visibility: "private",
                isReadOnly: true,
                isConstructorParameter: true),
        };

        var methods = new List<DeclarationNode>();
        foreach (var op in client.Operations)
        {
            methods.Add(BuildOperationMethod(op, importScan));
        }
        methods.Add(KotlinCancellableCallAstBuilder.Build());

        var classNode = new KotlinClassDeclarationNode(
            new IdentifierNode(className),
            ctorProps,
            methods,
            annotations: null,
            visibility: null,
            documentation: new CommentNode($"HTTP client for {client.Name}.", CommentKind.MultiLine));

        // Standard imports: always OkHttp + coroutines + kotlinx.serialization.json.
        var imports = new List<KotlinImportNode>
        {
            new KotlinImportNode("kotlinx.coroutines.Dispatchers"),
            new KotlinImportNode("kotlinx.coroutines.withContext"),
            new KotlinImportNode("kotlinx.coroutines.CancellableContinuation"),
            new KotlinImportNode("kotlinx.coroutines.suspendCancellableCoroutine"),
            new KotlinImportNode("kotlin.coroutines.CoroutineContext"),
            new KotlinImportNode("kotlinx.serialization.json.Json"),
            new KotlinImportNode("java.io.IOException"),
            new KotlinImportNode("okhttp3.Call"),
            new KotlinImportNode("okhttp3.Callback"),
            new KotlinImportNode("okhttp3.Response"),
            new KotlinImportNode("okhttp3.ResponseBody.Companion.toResponseBody"),
            new KotlinImportNode("okhttp3.HttpUrl"),
            new KotlinImportNode("okhttp3.MediaType.Companion.toMediaType"),
            new KotlinImportNode("okhttp3.OkHttpClient"),
            new KotlinImportNode("okhttp3.Request"),
            new KotlinImportNode("okhttp3.RequestBody.Companion.toRequestBody"),
        };
        foreach (var stdImport in importScan.StandardImports)
            imports.Add(new KotlinImportNode(stdImport));
        foreach (var modelName in importScan.ModelReferences)
        {
            // modelName carries the affix and is matched against the affixed known names.
            if (!knownModelNames.Contains(modelName)) continue;
            // Kotlin imports resolve by fully-qualified CLASS name (package + affixed type name),
            // independent of the .kt file name — so the import uses the affixed identifier.
            imports.Add(new KotlinImportNode($"{modelsPackage}.{modelName}"));
        }

        return new KotlinFileNode(apiPackage, declarations: [classNode], imports: imports);
    }

    /// <summary>Builds a public suspend operation preserving its source payload types.</summary>
    /// <param name="op">The source operation.</param>
    /// <param name="importScan">The recursive type imports for this client.</param>
    /// <returns>The operation declaration.</returns>
    private KotlinFunctionDeclarationNode BuildOperationMethod(
        IrOperation op,
        KotlinTypeImportScanner.Result importScan)
    {
        var methodName = KotlinReservedNames.Avoid(namingConverter.ToCamelCase(op.MethodName));
        var parameters = new List<KotlinParameterNode>();

        foreach (var p in op.PathParameters)
        {
            KotlinTypeImportScanner.Walk(p.Type, importScan);
            parameters.Add(new KotlinParameterNode(
                new IdentifierNode(KotlinReservedNames.Avoid(namingConverter.ToCamelCase(p.Name))),
                typeMapper.MapType(p.Type)));
        }

        if (op.RequestBody != null)
        {
            KotlinTypeImportScanner.Walk(op.RequestBody.Type, importScan);
            parameters.Add(new KotlinParameterNode(
                new IdentifierNode("body"),
                typeMapper.MapType(op.RequestBody.Type)));
        }

        foreach (var p in op.QueryParameters)
        {
            KotlinTypeImportScanner.Walk(p.Type, importScan);
            var annotation = typeMapper.MapType(p.Type);
            string? defaultValue = null;
            if (!p.IsRequired)
            {
                annotation = annotation is NullableTypeNode ? annotation : new NullableTypeNode(annotation);
                defaultValue = "null";
            }
            parameters.Add(new KotlinParameterNode(
                new IdentifierNode(KotlinReservedNames.Avoid(namingConverter.ToCamelCase(p.Name))),
                annotation,
                defaultValue));
        }

        // Pick the first 2xx response as success.
        var successResponse = op.Responses.FirstOrDefault(r => r.StatusCode.StartsWith("2", StringComparison.Ordinal));
        TypeNode returnType;
        string? responseModelName = null;
        string? responseModelNameForList = null;
        bool isVoidReturn = false;
        bool isBinaryReturn = false;
        if (successResponse?.Type != null)
        {
            KotlinTypeImportScanner.Walk(successResponse.Type, importScan);
            returnType = typeMapper.MapType(successResponse.Type);
            responseModelName = TryGetSimpleModelName(successResponse.Type);
            responseModelNameForList = TryGetArrayOfSimpleModelName(successResponse.Type);
            isBinaryReturn = successResponse.Type is IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File };
        }
        else
        {
            returnType = new SimpleTypeNode(new IdentifierNode("Unit"), isPrimitive: true);
            isVoidReturn = true;
        }

        var body = BuildMethodBody(op, responseModelName, responseModelNameForList, isVoidReturn, isBinaryReturn);

        return new KotlinFunctionDeclarationNode(
            new IdentifierNode(methodName),
            parameters,
            returnType,
            isSuspend: true,
            annotations: null,
            visibility: null,
            documentation: BuildOperationDocstring(op),
            expressionBody: body);
    }

    /// <summary>Preserves operation descriptions in generated Kotlin documentation.</summary>
    /// <param name="op">The source operation.</param>
    /// <returns>The operation comment.</returns>
    private static CommentNode? BuildOperationDocstring(IrOperation op)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(op.Summary)) sb.Append(op.Summary!.Trim());
        if (!string.IsNullOrWhiteSpace(op.Description))
        {
            if (sb.Length > 0) sb.AppendLine().AppendLine();
            sb.Append(op.Description!.Trim());
        }
        if (sb.Length == 0) sb.Append($"{op.HttpMethod} {op.Path}");
        return new CommentNode(sb.ToString(), CommentKind.MultiLine);
    }

    /// <summary>Builds a structured OkHttp request and decodes its response inside the cancellation-aware call.</summary>
    /// <param name="op">The source operation.</param>
    /// <param name="responseModelName">The direct response model name.</param>
    /// <param name="responseModelNameForList">The response collection element model name.</param>
    /// <param name="isVoidReturn">Whether the operation has no response payload.</param>
    /// <param name="isBinaryReturn">Whether to return response bytes.</param>
    /// <returns>The coroutine context expression with its nested response lambda.</returns>
    private ExpressionNode BuildMethodBody(IrOperation op, string? responseModelName, string? responseModelNameForList,
        bool isVoidReturn, bool isBinaryReturn)
    {
        var route = OpenApiPathExpressionBuilder.Build(op, parameter =>
            Ref(KotlinReservedNames.Avoid(namingConverter.ToCamelCase(parameter.Name))));
        var path = route.Parts.ToList();
        if (path.Count > 0 && path[0] is LiteralExpressionNode { Value: string leading })
            path[0] = Text(leading.TrimStart('/'));

        var urlBuilder = Ref("urlBuilder");
        var statements = new List<StatementNode>
        {
            Let("urlBuilder", Call(Member(Ref("baseUrl"), "newBuilder"))),
            Statement(Call(Member(urlBuilder, "addPathSegments"), new InterpolatedStringExpressionNode(path))),
        };

        foreach (var parameter in op.QueryParameters)
        {
            var argument = Ref(KotlinReservedNames.Avoid(namingConverter.ToCamelCase(parameter.Name)));
            var add = Statement(Call(Member(urlBuilder, "addQueryParameter"),
                Text(parameter.OriginalName), Call(Member(argument, "toString"))));
            var present = new BinaryExpressionNode(argument, BinaryOperator.NotEqual, Null());
            statements.Add(parameter.IsRequired ? add : new IfStatementNode(present, new BlockNode([add])));
        }

        statements.Add(Let("url", Call(Member(urlBuilder, "build"))));
        var createRequest = Call(Member(Ref("Request"), "Builder"));
        statements.Add(Let("requestBuilder", Call(Member(createRequest, "url"), Ref("url"))));

        var requestBuilder = Ref("requestBuilder");
        var verb = op.HttpMethod.ToUpperInvariant();
        ExpressionNode requestBody = Null();
        if (op.RequestBody is { } sourceBody)
        {
            if (sourceBody.IsJson)
            {
                var model = TryGetSimpleModelName(sourceBody.Type);
                var json = Member(Ref("json"), "encodeToString");
                var body = Ref("body");
                var encode = model is null ? Call(json, body) : Call(json, Serializer(model), body);
                statements.Add(Let("jsonBody", encode));
                statements.Add(Let("requestBody", ToRequestBody(Ref("jsonBody"), "application/json")));
            }
            else
            {
                var plain = Coalesce(Call(Member(Ref("body"), "toString", safe: true)), Text(""));
                statements.Add(Let("requestBody", ToRequestBody(plain, "application/octet-stream")));
            }
            requestBody = Ref("requestBody");
        }
        else if (verb is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            requestBody = ToRequestBody(Text(""), "application/json");
        }
        statements.Add(Statement(Call(Member(requestBuilder, "method"), Text(verb), requestBody)));
        statements.Add(Let("request", Call(Member(requestBuilder, "build"))));

        var execute = Call(Ref(KotlinCancellableCallAstBuilder.MethodName), Ref("request"));
        var use = new KotlinTrailingLambdaCallNode(Call(Member(execute, "use")),
            BuildResponse(responseModelName, responseModelNameForList, isVoidReturn, isBinaryReturn), new("response"));
        statements.Add(Statement(use));
        return new KotlinTrailingLambdaCallNode(Call(Ref("withContext"), Member(Ref("Dispatchers"), "IO")),
            new BlockNode(statements));
    }

    /// <summary>Checks the response and turns the received payload into the declared value.</summary>
    /// <param name="model">The direct response model, if present.</param>
    /// <param name="listModel">The response list element model, if present.</param>
    /// <param name="isVoidReturn">Whether completion itself is the result.</param>
    /// <param name="isBinaryReturn">Whether the caller receives raw bytes.</param>
    /// <returns>The statements and result inside the resource scope.</returns>
    private static BlockNode BuildResponse(string? model, string? listModel, bool isVoidReturn, bool isBinaryReturn)
    {
        var response = Ref("response");
        var failure = new UnaryExpressionNode(UnaryOperator.LogicalNot, Member(response, "isSuccessful"));
        var message = new InterpolatedStringExpressionNode([Text("HTTP "), Member(response, "code"), Text(": "), Member(response, "message")]);
        var statements = new List<StatementNode>
        {
            new IfStatementNode(failure, new BlockNode([Statement(Call(Ref("error"), message))])),
        };
        var responseBody = Member(response, "body");
        if (isVoidReturn)
        {
            statements.Add(Statement(Ref("Unit")));
        }
        else if (isBinaryReturn)
        {
            var fallback = Call(Ref("ByteArray"), new LiteralExpressionNode(0, LiteralKind.Number));
            statements.Add(Statement(Coalesce(Call(Member(responseBody, "bytes", safe: true)), fallback)));
        }
        else
        {
            var raw = Coalesce(Call(Member(responseBody, "string", safe: true)), Call(Ref("error"), Text("Empty body")));
            if (model is null && listModel is null)
            {
                statements.Add(Statement(raw));
            }
            else
            {
                statements.Add(Let("raw", raw));
                ExpressionNode codec = model is not null ? Serializer(model) :
                    Call(Ref("kotlinx.serialization.builtins.ListSerializer"), Serializer(listModel!));
                statements.Add(Statement(Call(Member(Ref("json"), "decodeFromString"), codec, Ref("raw"))));
            }
        }
        return new BlockNode(statements);
    }

    /// <summary>Builds the serialization call on a known model type.</summary>
    /// <param name="model">The referenced Kotlin model.</param>
    /// <returns>The call returning a serializer.</returns>
    private static CallExpressionNode Serializer(string model) => Call(Member(Ref(model), "serializer"));

    /// <summary>Converts a string to an OkHttp body with the given media type.</summary>
    /// <param name="source">The body content.</param>
    /// <param name="mime">The wire media type.</param>
    /// <returns>The body conversion call.</returns>
    private static CallExpressionNode ToRequestBody(ExpressionNode source, string mime) =>
        Call(Member(source, "toRequestBody"), Call(Member(Text(mime), "toMediaType")));

    /// <summary>Builds a fallback for a null expression.</summary>
    /// <param name="value">The expression to evaluate first.</param>
    /// <param name="fallback">The expression evaluated for null.</param>
    /// <returns>The Kotlin Elvis operation.</returns>
    private static BinaryExpressionNode Coalesce(ExpressionNode value, ExpressionNode fallback) =>
        new(value, BinaryOperator.NullishCoalescing, fallback);

    /// <summary>Declares an immutable binding inside a Kotlin block.</summary>
    /// <param name="name">The binding identifier.</param>
    /// <param name="value">The initialization expression.</param>
    /// <returns>The declaration statement.</returns>
    private static VariableDeclarationStatementNode Let(string name, ExpressionNode value) => new(new(name), initializer: value);

    /// <summary>Evaluates a statement or the final value of a block.</summary>
    /// <param name="expression">The value to evaluate.</param>
    /// <returns>The statement that contains the expression.</returns>
    private static ExpressionStatementNode Statement(ExpressionNode expression) => new(expression);

    /// <summary>References an identifier or qualified Kotlin symbol.</summary>
    /// <param name="name">The emitted name.</param>
    /// <returns>The identifier expression.</returns>
    private static IdentifierExpressionNode Ref(string name) => new(new IdentifierNode(name));

    /// <summary>Selects a Kotlin member with ordinary or null-safe access.</summary>
    /// <param name="target">The receiver.</param>
    /// <param name="name">The requested member.</param>
    /// <param name="safe">Whether to short-circuit for a null receiver.</param>
    /// <returns>The member expression.</returns>
    private static MemberAccessExpressionNode Member(ExpressionNode target, string name, bool safe = false) =>
        new(target, new(name), useOptionalChaining: safe);

    /// <summary>Invokes a selected method or constructor.</summary>
    /// <param name="target">The callable expression.</param>
    /// <param name="arguments">The positional arguments.</param>
    /// <returns>The invocation node.</returns>
    private static CallExpressionNode Call(ExpressionNode target, params ExpressionNode[] arguments) => new(target, arguments);

    /// <summary>Captures a wire value as data for the Kotlin printer.</summary>
    /// <param name="value">The original text.</param>
    /// <returns>A string literal expression.</returns>
    private static LiteralExpressionNode Text(string value) => new(value, LiteralKind.String);

    /// <summary>Represents the Kotlin null literal.</summary>
    /// <returns>A null literal expression.</returns>
    private static LiteralExpressionNode Null() => new(null, LiteralKind.Null);

    /// <summary>Finds a model reference beneath nullable or optional wrappers.</summary>
    /// <param name="type">The response type.</param>
    /// <returns>The model name, or null for another value shape.</returns>
    private static string? TryGetSimpleModelName(IrType type) => type switch
    {
        IrModelReference r => r.ModelName,
        IrNullableType n   => TryGetSimpleModelName(n.InnerType),
        IrOptionalType o   => TryGetSimpleModelName(o.InnerType),
        _ => null,
    };

    /// <summary>Returns the inner model name when <paramref name="type"/> is <c>List&lt;Model&gt;</c>.</summary>
    /// <param name="type">The response type.</param>
    /// <returns>The model collection element name, or null.</returns>
    private static string? TryGetArrayOfSimpleModelName(IrType type) => type switch
    {
        IrArrayType a       => TryGetSimpleModelName(a.ElementType),
        IrNullableType n    => TryGetArrayOfSimpleModelName(n.InnerType),
        IrOptionalType o    => TryGetArrayOfSimpleModelName(o.InnerType),
        _ => null,
    };

    /// <summary>Derives the generated service class identifier.</summary>
    /// <param name="clientName">The source group name.</param>
    /// <returns>The Kotlin API class name.</returns>
    private string ToClassName(string clientName)
    {
        var pascal = namingConverter.ToPascalCase(clientName);
        return pascal.EndsWith("Api", StringComparison.Ordinal) ? pascal : pascal + "Api";
    }
}
