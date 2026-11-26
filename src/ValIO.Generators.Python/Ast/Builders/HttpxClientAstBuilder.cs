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
using ValIO.Core.IR.Utilities;
using ValIO.Generators.Python.Ast.Nodes;
using ValIO.Generators.Python.Options;

namespace ValIO.Generators.Python.Ast.Builders;

/// <summary>
/// Builds HTTPX client facades that borrow the application's configured transport and authentication pipeline.
/// </summary>
/// <param name="documentContext">The finalized document context shared by the generation request.</param>
/// <param name="namingConverter">The converter for generated identifiers.</param>
/// <param name="typeMapper">The mapper for request and response annotations.</param>
public sealed class HttpxClientAstBuilder(IrDocumentContext documentContext, INamingConverter namingConverter, PythonIrTypeMapper typeMapper) : AstBuilderBase(documentContext)
{

    /// <summary>Builds a client module using runtime imports for request and response validation.</summary>
    /// <param name="client">The grouped operations to generate.</param>
    /// <param name="options">The Python client generation mode.</param>
    /// <returns>The generated module containing an injected HTTPX client facade.</returns>
    /// <exception cref="NotSupportedException">An operation declares incompatible successful response bodies.</exception>
    public PythonModuleNode BuildModule(
        IrClient client,
        PythonGenerationOptions options)
    {
        var isAsync = options.HttpxMode == PythonOptionIds.HttpxMode.Async;
        var className = ToClassName(client.Name);

        var modelImports = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var datetimeImports = new SortedSet<string>(StringComparer.Ordinal);
        var uuidNeeded = false;
        var anyNeeded = false;

        var methods = new List<DeclarationNode>();
        foreach (var op in client.Operations)
        {
            var method = BuildOperationMethod(
                op,
                isAsync,
                modelImports,
                datetimeImports,
                ref uuidNeeded,
                ref anyNeeded);
            methods.Add(method);
        }

        var ctor = BuildConstructor(isAsync);
        methods.Insert(0, ctor);

        var imports = new List<PythonImportNode>();

        var typingNames = new List<string> { "get_type_hints" };
        if (anyNeeded) typingNames.Add("Any");
        if (typingNames.Count > 0)
            imports.Add(new PythonImportNode("typing", typingNames));

        if (datetimeImports.Count > 0)
            imports.Add(new PythonImportNode("datetime", datetimeImports.ToList()));

        if (uuidNeeded)
            imports.Add(new PythonImportNode("uuid", new[] { "UUID" }));

        imports.Add(new PythonImportNode("httpx", names: Array.Empty<string>()));
        imports.Add(new PythonImportNode("pydantic", new[] { "TypeAdapter" }));
        if (client.Operations.Any(operation => operation.PathParameters.Any(
                parameter => IrTypeQueries.UnwrapNullableOptional(parameter.Type) is not IrPrimitiveType
                    { PrimitiveKind: IrPrimitiveKind.Integer or IrPrimitiveKind.Long or IrPrimitiveKind.Number })))
            imports.Add(new PythonImportNode("urllib.parse", new[] { "quote" }));

        foreach (var kvp in modelImports)
            imports.Add(new PythonImportNode(kvp.Value, new[] { kvp.Key }));

        var typeCheckingImports = new List<PythonImportNode>();

        var classNode = new PythonClassDeclarationNode(
            new IdentifierNode(className),
            baseClasses: null,
            members: methods,
            documentation: BuildClientDocstring(client));

        return new PythonModuleNode(
            declarations: new DeclarationNode[] { classNode },
            imports: imports,
            typeCheckingImports: typeCheckingImports,
            futureImports: new[] { new PythonImportNode("__future__", new[] { "annotations" }) });
    }

    /// <summary>Documents the caller-owned HTTPX pipeline and cancellation behavior.</summary>
    /// <param name="client">The source client group.</param>
    /// <returns>The generated class documentation.</returns>
    private static CommentNode BuildClientDocstring(IrClient client) =>
        new CommentNode($"HTTP client for {client.Name}.\n\nBorrows the supplied HTTPX client; the caller owns its lifetime.\nConfigure headers, Auth, event_hooks, and timeouts on that client.\nAsync calls propagate asyncio cancellation through the HTTPX transport.", CommentKind.MultiLine);

    /// <summary>Builds a constructor borrowing an existing HTTPX client without closing it.</summary>
    /// <param name="isAsync">Whether the facade uses an asynchronous HTTPX client.</param>
    /// <returns>The generated injected-client constructor.</returns>
    private static PythonFunctionDeclarationNode BuildConstructor(bool isAsync)
    {
        var clientAnnotation = new SimpleTypeNode(
            new IdentifierNode(isAsync ? "httpx.AsyncClient" : "httpx.Client"),
            isPrimitive: true);

        var parameters = new[]
        {
            new PythonParameterNode(new IdentifierNode("self")),
            new PythonParameterNode(new IdentifierNode("client"), clientAnnotation),
        };

        return new PythonFunctionDeclarationNode(
            new IdentifierNode("__init__"),
            parameters,
            returnType: new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true),
            body: new BlockNode([Assign(Member(Ref("self"), "_client"), Ref("client"))]));
    }

    /// <summary>Builds a typed operation whose transport calls use the borrowed HTTPX pipeline.</summary>
    /// <param name="op">The operation to generate.</param>
    /// <param name="isAsync">Whether transport calls are awaited.</param>
    /// <param name="modelImports">The runtime declaration imports collected for the module.</param>
    /// <param name="datetimeImports">The required standard date and time imports.</param>
    /// <param name="uuidNeeded">Whether UUID annotations require an import.</param>
    /// <param name="anyNeeded">Whether unconstrained annotations require an import.</param>
    /// <returns>The generated Python operation declaration.</returns>
    /// <exception cref="NotSupportedException">Successful response bodies have incompatible types.</exception>
    private PythonFunctionDeclarationNode BuildOperationMethod(
        IrOperation op,
        bool isAsync,
        SortedDictionary<string, string> modelImports,
        SortedSet<string> datetimeImports,
        ref bool uuidNeeded,
        ref bool anyNeeded)
    {
        var methodName = namingConverter.ToSnakeCase(op.MethodName);
        methodName = PythonKeywords.Avoid(methodName);

        var parameters = new List<PythonParameterNode>
        {
            new PythonParameterNode(new IdentifierNode("self"))
        };

        foreach (var p in op.PathParameters)
        {
            var annotation = typeMapper.MapType(p.Type);
            ScanForImports(p.Type, modelImports,
                datetimeImports, ref uuidNeeded, ref anyNeeded);
            parameters.Add(new PythonParameterNode(
                new IdentifierNode(PythonKeywords.Avoid(namingConverter.ToSnakeCase(p.Name))),
                annotation));
        }

        if (op.RequestBody is { IsRequired: true })
        {
            var annotation = typeMapper.MapType(op.RequestBody.Type);
            ScanForImports(op.RequestBody.Type, modelImports,
                datetimeImports, ref uuidNeeded, ref anyNeeded);
            parameters.Add(new PythonParameterNode(
                new IdentifierNode("body"),
                annotation));
        }

        foreach (var p in op.QueryParameters.Concat(op.HeaderParameters).OrderBy(parameter => !parameter.IsRequired))
        {
            var annotation = typeMapper.MapType(p.Type);
            ScanForImports(p.Type, modelImports,
                datetimeImports, ref uuidNeeded, ref anyNeeded);
            string? defaultValue = null;
            if (!p.IsRequired)
            {
                annotation = AddNone(annotation);
                defaultValue = "None";
            }
            parameters.Add(new PythonParameterNode(
                new IdentifierNode(PythonKeywords.Avoid(namingConverter.ToSnakeCase(p.Name))),
                annotation,
                defaultValue));
        }

        if (op.RequestBody is { IsRequired: false } optionalBody)
        {
            var annotation = AddNone(typeMapper.MapType(optionalBody.Type));
            ScanForImports(optionalBody.Type, modelImports,
                datetimeImports, ref uuidNeeded, ref anyNeeded);
            parameters.Add(new PythonParameterNode(new IdentifierNode("body"), annotation, "None"));
        }

        var successfulResponses = op.Responses.Where(response => response.StatusCode.StartsWith("2", StringComparison.Ordinal)).ToArray();
        var successResponse = successfulResponses.FirstOrDefault(response => response.Type is not null);
        if (successResponse?.Type is { } selectedType && successfulResponses.Any(response =>
                response.Type is not null && !IrTypeComparer.AreSemanticallyEqual(selectedType, response.Type, ignorePresence: false)))
            throw new NotSupportedException($"Python operation '{op.MethodName}' declares incompatible successful response bodies.");
        TypeNode? returnType = null;
        string? responseModelName = null;
        if (successResponse?.Type != null)
        {
            returnType = typeMapper.MapType(successResponse.Type);
            ScanForImports(successResponse.Type, modelImports,
                datetimeImports, ref uuidNeeded, ref anyNeeded);
            var referencedName = TryGetSimpleModelName(successResponse.Type);
            responseModelName = referencedName is not null && ModelsByName.ContainsKey(referencedName) ? referencedName : null;
            if (successfulResponses.Any(response => response.Type is null)) returnType = AddNone(returnType);
        }
        else
        {
            returnType = new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true);
        }

        var body = BuildMethodBody(op, isAsync, responseModelName, successResponse?.Type, methodName);

        return new PythonFunctionDeclarationNode(
            new IdentifierNode(methodName),
            parameters,
            returnType: returnType,
            body: body,
            isAsync: isAsync,
            documentation: BuildOperationDocstring(op));
    }

    /// <summary>Combines source documentation into a generated operation docstring.</summary>
    /// <param name="op">The operation containing the source summary and description.</param>
    /// <returns>The operation documentation, falling back to its HTTP method and path.</returns>
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

    /// <summary>Builds HTTPX wire calls and validates successful typed response bodies.</summary>
    /// <param name="op">The operation with source wire names and transport metadata.</param>
    /// <param name="isAsync">Whether to await the HTTPX operation.</param>
    /// <param name="responseModelName">The direct Pydantic response model, if any.</param>
    /// <param name="responseType">The declared success payload.</param>
    /// <param name="methodName">The generated method whose annotations supply adapter types.</param>
    /// <returns>The function statements in execution order.</returns>
    private BlockNode BuildMethodBody(IrOperation op, bool isAsync, string? responseModelName, IrType? responseType, string methodName)
    {
        var statements = new List<StatementNode>();
        var path = OpenApiPathExpressionBuilder.Build(op, PathParameter);
        var arguments = new List<ExpressionNode> { path };
        if (op.QueryParameters.Count > 0)
        {
            statements.AddRange(WriteWireParameters("params", op.QueryParameters, toString: false));
            arguments.Add(Keyword("params", Ref("params")));
        }
        if (op.HeaderParameters.Count > 0)
        {
            statements.AddRange(WriteWireParameters("headers", op.HeaderParameters, toString: true));
            arguments.Add(Keyword("headers", Ref("headers")));
        }

        if (op.RequestBody is { } requestBody)
        {
            var body = Ref("body");
            if (requestBody.IsJson || requestBody.ContentType is null)
            {
                var adapter = TypeAdapter(methodName, "body");
                var encoded = Call(Member(adapter, "dump_python"), body, Keyword("mode", Text("json")),
                    Keyword("by_alias", Boolean(true)), Keyword("exclude_unset", Boolean(true)));
                if (requestBody.IsRequired)
                {
                    arguments.Add(Keyword("json", encoded));
                }
                else
                {
                    var optional = new ConditionalExpressionNode(Present(body),
                        new PythonDictionaryExpressionNode([(Text("json"), encoded)]), new PythonDictionaryExpressionNode([]));
                    statements.Add(Local("body_args", optional));
                    arguments.Add(new PythonKeywordUnpackNode(Ref("body_args")));
                }
            }
            else
            {
                var argument = requestBody.IsFormUrlEncoded ? "data" : requestBody.IsMultipartFormData ? "files" : "content";
                arguments.Add(Keyword(argument, body));
            }
        }

        var client = Member(Ref("self"), "_client");
        var send = new CallExpressionNode(Member(client, op.HttpMethod.ToLowerInvariant()), arguments);
        statements.Add(Local("response", isAsync ? new AwaitExpressionNode(send) : send));
        var response = Ref("response");
        statements.Add(new ExpressionStatementNode(Call(Member(response, "raise_for_status"))));

        var status = Member(response, "status_code");
        var request = Member(response, "request");
        var bodylessStatus = new BinaryExpressionNode(Equal(status, Number(204)), BinaryOperator.LogicalOr, Equal(status, Number(205)));
        var bodyless = new BinaryExpressionNode(bodylessStatus, BinaryOperator.LogicalOr, Equal(Member(request, "method"), Text("HEAD")));
        statements.Add(new IfStatementNode(bodyless, new BlockNode([new ReturnStatementNode(None())])));

        var json = Call(Member(response, "json"));
        ExpressionNode value;
        if (responseModelName is not null)
        {
            var validate = Member(Ref(responseModelName), "model_validate");
            if (responseType is IrNullableType or IrOptionalType)
            {
                statements.Add(Local("payload", json));
                var payload = Ref("payload");
                var absent = new BinaryExpressionNode(payload, BinaryOperator.Is, None());
                value = new ConditionalExpressionNode(absent, None(), Call(validate, payload));
            }
            else
                value = Call(validate, json);
        }
        else
        {
            value = responseType switch
            {
                null or IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Void or IrPrimitiveKind.Null } => None(),
                IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File } =>
                    Member(response, "content"),
                _ => Call(Member(TypeAdapter(methodName, "return"), "validate_python"), json),
            };
        }
        statements.Add(new ReturnStatementNode(value));
        return new BlockNode(statements);
    }

    /// <summary>Places present arguments in a wire dictionary, retaining their original names.</summary>
    /// <param name="dictionary">The local dictionary identifier.</param>
    /// <param name="parameters">The query or header parameter declarations.</param>
    /// <param name="toString">Whether transport values must be strings.</param>
    /// <returns>The declaration followed by the assignments for supplied values.</returns>
    private IEnumerable<StatementNode> WriteWireParameters(string dictionary, IEnumerable<IrParameter> parameters, bool toString)
    {
        yield return Local(dictionary, new PythonDictionaryExpressionNode([]));
        foreach (var parameter in parameters)
        {
            var argument = Ref(ParameterName(parameter));
            var target = new IndexAccessExpressionNode(Ref(dictionary), Text(parameter.OriginalName));
            var assignment = Assign(target, toString ? Call(Ref("str"), argument) : argument);
            yield return parameter.IsRequired ? assignment : new IfStatementNode(Present(argument), new BlockNode([assignment]));
        }
    }

    /// <summary>Converts textual placeholders to path segments through percent encoding.</summary>
    /// <param name="parameter">The operation parameter referenced by the path.</param>
    /// <returns>The formatted argument expression.</returns>
    private ExpressionNode PathParameter(IrParameter parameter)
    {
        var value = Ref(ParameterName(parameter));
        return IrTypeQueries.UnwrapNullableOptional(parameter.Type) is IrPrimitiveType
            { PrimitiveKind: IrPrimitiveKind.Integer or IrPrimitiveKind.Long or IrPrimitiveKind.Number }
            ? value : Call(Ref("quote"), Call(Ref("str"), value), Keyword("safe", Text("")));
    }

    /// <summary>Uses the operation annotation as the Pydantic adapter's exact expected type.</summary>
    /// <param name="method">The generated operation identifier.</param>
    /// <param name="annotation">The annotation key to use.</param>
    /// <returns>The adapter constructor call.</returns>
    private static CallExpressionNode TypeAdapter(string method, string annotation)
    {
        var hints = Call(Ref("get_type_hints"), Member(Ref("self"), method));
        var expected = new IndexAccessExpressionNode(hints, Text(annotation));
        return Call(Ref("TypeAdapter"), expected);
    }

    /// <summary>Gets the generated identifier shared by a formal parameter and its references.</summary>
    /// <param name="parameter">The wire parameter.</param>
    /// <returns>The valid Python name.</returns>
    private string ParameterName(IrParameter parameter) => PythonKeywords.Avoid(namingConverter.ToSnakeCase(parameter.Name));

    /// <summary>Creates a local binding for the supplied value.</summary>
    /// <param name="name">The local variable name.</param>
    /// <param name="initializer">The expression assigned to the variable.</param>
    /// <returns>The local declaration.</returns>
    private static VariableDeclarationStatementNode Local(string name, ExpressionNode initializer) =>
        new(new(name), initializer: initializer);

    /// <summary>Assigns a Python expression to a destination.</summary>
    /// <param name="target">The variable, field or subscript.</param>
    /// <param name="value">The expression written to it.</param>
    /// <returns>The assignment statement.</returns>
    private static ExpressionStatementNode Assign(ExpressionNode target, ExpressionNode value) =>
        new(new BinaryExpressionNode(target, BinaryOperator.Assign, value));

    /// <summary>Binds a named argument at a call site.</summary>
    /// <param name="name">The argument name.</param>
    /// <param name="value">The supplied value.</param>
    /// <returns>The keyword argument expression.</returns>
    private static BinaryExpressionNode Keyword(string name, ExpressionNode value) =>
        new(Ref(name), BinaryOperator.Assign, value, isNamedArgument: true);

    /// <summary>Tests whether a value is distinct from None.</summary>
    /// <param name="value">The optional value.</param>
    /// <returns>The presence test expression.</returns>
    private static BinaryExpressionNode Present(ExpressionNode value) => new(value, BinaryOperator.IsNot, None());

    /// <summary>Compares two Python values for equality.</summary>
    /// <param name="left">The observed value.</param>
    /// <param name="right">The expected value.</param>
    /// <returns>The equality expression.</returns>
    private static BinaryExpressionNode Equal(ExpressionNode left, ExpressionNode right) => new(left, BinaryOperator.Equal, right);

    /// <summary>References a Python identifier.</summary>
    /// <param name="name">The identifier in scope.</param>
    /// <returns>The reference expression.</returns>
    private static IdentifierExpressionNode Ref(string name) => new(new IdentifierNode(name));

    /// <summary>Selects a member of a Python expression.</summary>
    /// <param name="expression">The receiver.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The selected member.</returns>
    private static MemberAccessExpressionNode Member(ExpressionNode expression, string name) => new(expression, new(name));

    /// <summary>Invokes a Python expression with positional and keyword arguments.</summary>
    /// <param name="target">The callable expression.</param>
    /// <param name="arguments">The call arguments.</param>
    /// <returns>The invocation expression.</returns>
    private static CallExpressionNode Call(ExpressionNode target, params ExpressionNode[] arguments) => new(target, arguments);

    /// <summary>Captures a wire string for literal escaping.</summary>
    /// <param name="value">The original wire value.</param>
    /// <returns>A string literal expression.</returns>
    private static LiteralExpressionNode Text(string value) => new(value, LiteralKind.String);

    /// <summary>Captures an HTTP status as a number.</summary>
    /// <param name="value">The numeric HTTP status.</param>
    /// <returns>The number literal expression.</returns>
    private static LiteralExpressionNode Number(int value) => new(value, LiteralKind.Number);

    /// <summary>Represents a constant boolean value.</summary>
    /// <param name="value">The flag value.</param>
    /// <returns>The boolean literal expression.</returns>
    private static LiteralExpressionNode Boolean(bool value) => new(value, LiteralKind.Boolean);

    /// <summary>Represents the Python None singleton.</summary>
    /// <returns>The null literal expression.</returns>
    private static LiteralExpressionNode None() => new(null, LiteralKind.Null);

    /// <summary>Finds a direct declaration reference beneath presence wrappers.</summary>
    /// <param name="type">The response type to inspect.</param>
    /// <returns>The referenced generated declaration name, or null for container and primitive types.</returns>
    private static string? TryGetSimpleModelName(IrType type) => type switch
    {
        IrModelReference r => r.ModelName,
        IrNullableType n => TryGetSimpleModelName(n.InnerType),
        IrOptionalType o => TryGetSimpleModelName(o.InnerType),
        _ => null
    };

    /// <summary>Adds nullability without repeating an existing None union arm.</summary>
    /// <param name="inner">The original type annotation.</param>
    /// <returns>The annotation accepting both its original values and None.</returns>
    private static TypeNode AddNone(TypeNode inner)
    {
        if (inner is UnionTypeNode u && u.Types.Any(t => t is SimpleTypeNode s && s.Name.Name == "None"))
            return inner;
        if (inner is UnionTypeNode existing)
            return new UnionTypeNode(existing.Types.Concat(new[] { (TypeNode)new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true) }).ToList());
        return new UnionTypeNode(new[] { inner, (TypeNode)new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true) });
    }

    /// <summary>Collects runtime and standard-library imports needed by request and response annotations.</summary>
    /// <param name="type">The schema type graph to traverse.</param>
    /// <param name="modelImports">The generated declaration imports receiving discovered references.</param>
    /// <param name="datetimeImports">The standard date and time imports.</param>
    /// <param name="uuidNeeded">Whether UUID annotations require an import.</param>
    /// <param name="anyNeeded">Whether unconstrained annotations require typing.Any.</param>
    private void ScanForImports(
        IrType type,
        SortedDictionary<string, string> modelImports,
        SortedSet<string> datetimeImports,
        ref bool uuidNeeded,
        ref bool anyNeeded)
    {
        switch (type)
        {
            case IrPrimitiveType p:
                switch (p.PrimitiveKind)
                {
                    case IrPrimitiveKind.Date: datetimeImports.Add("date"); break;
                    case IrPrimitiveKind.DateTime: datetimeImports.Add("datetime"); break;
                    case IrPrimitiveKind.Time: datetimeImports.Add("time"); break;
                    case IrPrimitiveKind.Uuid: uuidNeeded = true; break;
                    case IrPrimitiveKind.Any: anyNeeded = true; break;
                }
                break;
            case IrModelReference r:
                if (PythonDeclarationImports.TryGetModulePath(DocumentContext, namingConverter, r.ModelName, "..models.", out var module))
                    modelImports[r.ModelName] = module;
                break;
            case IrArrayType a:
                ScanForImports(a.ElementType, modelImports, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrDictionaryType d:
                ScanForImports(d.KeyType, modelImports, datetimeImports, ref uuidNeeded, ref anyNeeded);
                ScanForImports(d.ValueType, modelImports, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrNullableType n:
                ScanForImports(n.InnerType, modelImports, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrOptionalType o:
                ScanForImports(o.InnerType, modelImports, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrUnionType u:
                foreach (var t in u.Types) ScanForImports(t, modelImports, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrIntersectionType i:
                foreach (var t in i.Types) ScanForImports(t, modelImports, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
        }
    }

    /// <summary>Converts a grouped client name into a Python API class identifier.</summary>
    /// <param name="clientName">The source client group name.</param>
    /// <returns>A PascalCase identifier ending in Api.</returns>
    private string ToClassName(string clientName)
    {
        var pascal = namingConverter.ToPascalCase(clientName);
        return pascal.EndsWith("Api", StringComparison.Ordinal) ? pascal : pascal + "Api";
    }
}
