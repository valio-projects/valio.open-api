using System.Collections.Immutable;
using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.Java.Ast.Nodes;

namespace ValIO.Generators.Java.Ast.Builders;

/// <summary>
/// Строит <see cref="JavaSourceFileNode"/> с OkHttp-клиентом для одного IR <see cref="IrClient"/>.
/// </summary>
public sealed class OkHttpClientAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly JavaIrTypeMapper _typeMapper;

    public OkHttpClientAstBuilder(INamingConverter namingConverter, JavaIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
    }

    public JavaSourceFileNode BuildSourceFile(IrClient client, string rootPackage)
    {
        var pkg = NormalizePackage($"{rootPackage}.api");
        var className = BuildClassName(client.Name);

        var importTargets = new SortedSet<string>(StringComparer.Ordinal)
        {
            "com.fasterxml.jackson.databind.ObjectMapper",
            "okhttp3.HttpUrl",
            "okhttp3.OkHttpClient",
            "okhttp3.Request",
            "okhttp3.Response",
            "java.io.IOException",
        };
        var modelsPackage = NormalizePackage($"{rootPackage}.models");

        // Fields.
        var clientField = new PropertyDeclarationNode(
            name: new IdentifierNode("client"),
            type: new SimpleTypeNode(new IdentifierNode("OkHttpClient")),
            isReadOnly: true);
        var baseUrlField = new PropertyDeclarationNode(
            name: new IdentifierNode("baseUrl"),
            type: new SimpleTypeNode(new IdentifierNode("HttpUrl")),
            isReadOnly: true);
        var mapperField = new PropertyDeclarationNode(
            name: new IdentifierNode("mapper"),
            type: new SimpleTypeNode(new IdentifierNode("ObjectMapper")),
            isReadOnly: true);

        // Constructor.
        var ctorParams = new[]
        {
            new ParameterDeclarationNode(new IdentifierNode("client"),  new SimpleTypeNode(new IdentifierNode("OkHttpClient"))),
            new ParameterDeclarationNode(new IdentifierNode("baseUrl"), new SimpleTypeNode(new IdentifierNode("HttpUrl"))),
            new ParameterDeclarationNode(new IdentifierNode("mapper"),  new SimpleTypeNode(new IdentifierNode("ObjectMapper"))),
        };
        var ctorBody = new BlockNode(
        [
            new ExpressionStatementNode(AssignThis("client",  "client")),
            new ExpressionStatementNode(AssignThis("baseUrl", "baseUrl")),
            new ExpressionStatementNode(AssignThis("mapper",  "mapper")),
        ]);
        var ctor = new MethodDeclarationNode(
            name: new IdentifierNode(className),
            returnType: null,
            parameters: ctorParams,
            body: ctorBody,
            accessModifier: AccessModifier.Public);

        var methods = new List<DeclarationNode>();
        foreach (var op in client.Operations)
        {
            methods.Add(BuildOperationMethod(op, importTargets, modelsPackage));
        }

        var cls = new JavaClassDeclarationNode(
            name: new IdentifierNode(className),
            isFinal: true,
            fields: new[] { clientField, baseUrlField, mapperField },
            constructor: ctor,
            members: methods);

        var imports = importTargets
            .Select(t => new JavaImportNode(t))
            .ToImmutableList();

        return new JavaSourceFileNode(
            package: pkg,
            imports: imports,
            declarations: new DeclarationNode[] { cls }.ToImmutableList());
    }

    private MethodDeclarationNode BuildOperationMethod(IrOperation op, ISet<string> importTargets, string modelsPackage)
    {
        var methodName = _namingConverter.ToCamelCase(op.MethodName);

        var parameters = new List<ParameterDeclarationNode>();
        foreach (var p in op.PathParameters)
            parameters.Add(BuildParam(p, importTargets, modelsPackage));
        foreach (var p in op.QueryParameters)
            parameters.Add(BuildParam(p, importTargets, modelsPackage));
        foreach (var p in op.HeaderParameters)
            parameters.Add(BuildParam(p, importTargets, modelsPackage));
        if (op.RequestBody != null)
        {
            var bodyType = _typeMapper.MapType(op.RequestBody.Type, preferUnboxed: false);
            foreach (var im in _typeMapper.CollectImports(op.RequestBody.Type)) importTargets.Add(im);
            if (op.RequestBody.Type is IrModelReference bodyRef)
                importTargets.Add($"{modelsPackage}.{bodyRef.ModelName}");
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("body"),
                type: bodyType,
                isRequired: op.RequestBody.IsRequired));
        }

        var returnType = ResolveReturnType(op, importTargets, modelsPackage, out var returnModelName);

        var statements = new List<StatementNode>();

        // HttpUrl url = baseUrl.newBuilder().addPathSegments(...).build();
        var urlInit = BuildUrlExpression(op, parameters);
        statements.Add(new VariableDeclarationStatementNode(
            name: new IdentifierNode("url"),
            type: new SimpleTypeNode(new IdentifierNode("HttpUrl")),
            initializer: urlInit));

        // Request request = new Request.Builder().url(url).{method}(...).build();
        var requestInit = BuildRequestExpression(op, hasBody: op.RequestBody != null);
        statements.Add(new VariableDeclarationStatementNode(
            name: new IdentifierNode("request"),
            type: new SimpleTypeNode(new IdentifierNode("Request")),
            initializer: requestInit));

        // try (Response response = client.newCall(request).execute()) { ... }
        var responseInitExpr = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new CallExpressionNode(
                    target: new MemberAccessExpressionNode(
                        @object: new IdentifierExpressionNode(new IdentifierNode("client")),
                        member: new IdentifierNode("newCall")),
                    arguments: new ExpressionNode[] { new IdentifierExpressionNode(new IdentifierNode("request")) }),
                member: new IdentifierNode("execute")),
            arguments: ImmutableList<ExpressionNode>.Empty);
        var responseResource = $"Response response = {ExpressionToString(responseInitExpr)}";

        var tryStatements = new List<StatementNode>();
        if (returnType != null && returnModelName != null)
        {
            // return mapper.readValue(response.body().byteStream(), ReturnType.class);
            var classLiteral = new IdentifierExpressionNode(new IdentifierNode(returnModelName + ".class"));
            var readValueCall = new CallExpressionNode(
                target: new MemberAccessExpressionNode(
                    @object: new IdentifierExpressionNode(new IdentifierNode("mapper")),
                    member: new IdentifierNode("readValue")),
                arguments: new ExpressionNode[]
                {
                    new CallExpressionNode(
                        target: new MemberAccessExpressionNode(
                            @object: new CallExpressionNode(
                                target: new MemberAccessExpressionNode(
                                    @object: new IdentifierExpressionNode(new IdentifierNode("response")),
                                    member: new IdentifierNode("body")),
                                arguments: ImmutableList<ExpressionNode>.Empty),
                            member: new IdentifierNode("byteStream")),
                        arguments: ImmutableList<ExpressionNode>.Empty),
                    classLiteral,
                });
            tryStatements.Add(new ReturnStatementNode(readValueCall));
        }
        else
        {
            // response.close(); is implicit because of try-with-resources; just emit empty body / return.
        }
        var tryBlock = new BlockNode(tryStatements);
        var tryMetadata = new Dictionary<string, object>
        {
            ["withResources"] = new[] { responseResource }
        };
        var tryCatch = new TryCatchStatementNode(
            tryBlock: tryBlock,
            catchClauses: null,
            finallyBlock: null,
            documentation: null,
            metadata: tryMetadata);
        statements.Add(tryCatch);

        var body = new BlockNode(statements);

        var docLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(op.Summary))
            docLines.Add(op.Summary!);
        if (!string.IsNullOrWhiteSpace(op.Description))
        {
            if (docLines.Count > 0) docLines.Add("");
            docLines.Add(op.Description!);
        }
        var doc = docLines.Count > 0 ? new CommentNode(string.Join("\n", docLines), CommentKind.MultiLine) : null;

        var methodMetadata = new Dictionary<string, object>
        {
            ["throws"] = (IReadOnlyList<string>)new[] { "IOException" },
        };

        return new MethodDeclarationNode(
            name: new IdentifierNode(methodName),
            returnType: returnType ?? new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true),
            parameters: parameters,
            body: body,
            accessModifier: AccessModifier.Public,
            documentation: doc,
            metadata: methodMetadata);
    }

    private ParameterDeclarationNode BuildParam(IrParameter p, ISet<string> importTargets, string modelsPackage)
    {
        var typeNode = _typeMapper.MapType(p.Type, preferUnboxed: p.IsRequired);
        foreach (var im in _typeMapper.CollectImports(p.Type)) importTargets.Add(im);
        if (p.Type is IrModelReference modelRef)
            importTargets.Add($"{modelsPackage}.{modelRef.ModelName}");
        var name = _namingConverter.ToCamelCase(p.Name);
        if (JavaReservedWords.Reserved.Contains(name)) name += "_";
        return new ParameterDeclarationNode(
            name: new IdentifierNode(name),
            type: typeNode,
            isRequired: p.IsRequired);
    }

    private TypeNode? ResolveReturnType(IrOperation op, ISet<string> importTargets, string modelsPackage, out string? returnClassToken)
    {
        returnClassToken = null;
        // Pick the first 2xx success response with a payload; else void.
        var success = op.Responses.FirstOrDefault(r =>
            int.TryParse(r.StatusCode, out var code) && code >= 200 && code < 300 && r.Type != null);
        if (success?.Type == null)
        {
            return new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true);
        }

        var typeNode = _typeMapper.MapType(success.Type, preferUnboxed: false);
        foreach (var im in _typeMapper.CollectImports(success.Type)) importTargets.Add(im);

        // For model references, we know the class symbol; emit Type.class.
        if (success.Type is IrModelReference modelRef)
        {
            importTargets.Add($"{modelsPackage}.{modelRef.ModelName}");
            returnClassToken = modelRef.ModelName;
        }
        else if (typeNode is SimpleTypeNode simple)
        {
            // Primitive boxed: still mapper-readable with Boxed.class.
            returnClassToken = simple.Name.Name;
        }
        else if (typeNode is GenericTypeNode)
        {
            // Generic (List<X>, Map<K, V>): use Object.class fallback — caller can override.
            returnClassToken = "Object";
        }

        return typeNode;
    }

    private static MemberAccessExpressionNode BuildThis(string field) =>
        new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("this")),
            member: new IdentifierNode(field));

    private static BinaryExpressionNode AssignThis(string field, string value) =>
        new BinaryExpressionNode(
            left: BuildThis(field),
            @operator: BinaryOperator.Assign,
            right: new IdentifierExpressionNode(new IdentifierNode(value)));

    private ExpressionNode BuildUrlExpression(IrOperation op, IReadOnlyList<ParameterDeclarationNode> parameters)
    {
        // baseUrl.newBuilder().addPathSegments(<path expr>).addQueryParameter(...)...build()
        ExpressionNode chain = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("baseUrl")),
                member: new IdentifierNode("newBuilder")),
            arguments: ImmutableList<ExpressionNode>.Empty);

        chain = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: chain,
                member: new IdentifierNode("addPathSegments")),
            arguments: new ExpressionNode[] { BuildPathExpression(op) });

        foreach (var q in op.QueryParameters)
        {
            var paramName = _namingConverter.ToCamelCase(q.Name);
            if (JavaReservedWords.Reserved.Contains(paramName)) paramName += "_";
            ExpressionNode valueExpr = new IdentifierExpressionNode(new IdentifierNode(paramName));
            // Coerce non-strings to String via String.valueOf(...)
            if (!IsStringType(q.Type))
            {
                valueExpr = new CallExpressionNode(
                    target: new MemberAccessExpressionNode(
                        @object: new IdentifierExpressionNode(new IdentifierNode("String")),
                        member: new IdentifierNode("valueOf")),
                    arguments: new ExpressionNode[] { valueExpr });
            }
            chain = new CallExpressionNode(
                target: new MemberAccessExpressionNode(
                    @object: chain,
                    member: new IdentifierNode("addQueryParameter")),
                arguments: new ExpressionNode[]
                {
                    new LiteralExpressionNode(q.Name, LiteralKind.String),
                    valueExpr,
                });
        }

        chain = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: chain,
                member: new IdentifierNode("build")),
            arguments: ImmutableList<ExpressionNode>.Empty);

        return chain;
    }

    private ExpressionNode BuildPathExpression(IrOperation op)
    {
        // Build a Java string concatenation expression of literal segments and path params.
        // Strip leading slash to play nice with addPathSegments.
        var path = op.Path.TrimStart('/');
        if (!path.Contains('{'))
        {
            return new LiteralExpressionNode(path, LiteralKind.String);
        }

        var parts = new List<ExpressionNode>();
        var sb = new StringBuilder();
        int i = 0;
        while (i < path.Length)
        {
            if (path[i] == '{')
            {
                if (sb.Length > 0)
                {
                    parts.Add(new LiteralExpressionNode(sb.ToString(), LiteralKind.String));
                    sb.Clear();
                }
                var end = path.IndexOf('}', i);
                if (end < 0) { sb.Append(path[i++]); continue; }
                var paramName = path.Substring(i + 1, end - i - 1);
                var sanitized = _namingConverter.ToCamelCase(paramName);
                if (JavaReservedWords.Reserved.Contains(sanitized)) sanitized += "_";
                // Wrap with String.valueOf(...) to support any param type.
                parts.Add(new CallExpressionNode(
                    target: new MemberAccessExpressionNode(
                        @object: new IdentifierExpressionNode(new IdentifierNode("String")),
                        member: new IdentifierNode("valueOf")),
                    arguments: new ExpressionNode[] { new IdentifierExpressionNode(new IdentifierNode(sanitized)) }));
                i = end + 1;
            }
            else
            {
                sb.Append(path[i++]);
            }
        }
        if (sb.Length > 0) parts.Add(new LiteralExpressionNode(sb.ToString(), LiteralKind.String));

        // Chain "+" left-to-right; ensure first part is a string literal.
        if (parts.Count == 0) return new LiteralExpressionNode(string.Empty, LiteralKind.String);
        if (parts[0] is not LiteralExpressionNode)
            parts.Insert(0, new LiteralExpressionNode(string.Empty, LiteralKind.String));
        ExpressionNode current = parts[0];
        for (int j = 1; j < parts.Count; j++)
        {
            current = new BinaryExpressionNode(
                left: current,
                @operator: BinaryOperator.Add,
                right: parts[j]);
        }
        return current;
    }

    private ExpressionNode BuildRequestExpression(IrOperation op, bool hasBody)
    {
        // new Request.Builder().url(url).{method}(body|nothing).build()
        ExpressionNode chain = new CallExpressionNode(
            target: new IdentifierExpressionNode(new IdentifierNode("Request.Builder")),
            arguments: ImmutableList<ExpressionNode>.Empty,
            isNewExpression: true);

        chain = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: chain,
                member: new IdentifierNode("url")),
            arguments: new ExpressionNode[] { new IdentifierExpressionNode(new IdentifierNode("url")) });

        var method = op.HttpMethod.ToLowerInvariant();
        switch (method)
        {
            case "get":
            case "delete":
            case "head":
                chain = new CallExpressionNode(
                    target: new MemberAccessExpressionNode(
                        @object: chain,
                        member: new IdentifierNode(method)),
                    arguments: ImmutableList<ExpressionNode>.Empty);
                break;
            default:
                // For POST/PUT/PATCH: pass a RequestBody if provided, else use empty body.
                if (hasBody)
                {
                    // RequestBody.create(mapper.writeValueAsBytes(body), null)  — but Body is omitted from imports
                    // for now to keep generated code self-contained. We pass a placeholder body via okhttp's
                    // RequestBody.create with null media type and an empty byte array as fallback.
                    chain = new CallExpressionNode(
                        target: new MemberAccessExpressionNode(
                            @object: chain,
                            member: new IdentifierNode(method)),
                        arguments: new ExpressionNode[]
                        {
                            new CallExpressionNode(
                                target: new MemberAccessExpressionNode(
                                    @object: new IdentifierExpressionNode(new IdentifierNode("okhttp3.RequestBody")),
                                    member: new IdentifierNode("create")),
                                arguments: new ExpressionNode[]
                                {
                                    new CallExpressionNode(
                                        target: new MemberAccessExpressionNode(
                                            @object: new IdentifierExpressionNode(new IdentifierNode("mapper")),
                                            member: new IdentifierNode("writeValueAsBytes")),
                                        arguments: new ExpressionNode[]
                                        {
                                            new IdentifierExpressionNode(new IdentifierNode("body")),
                                        }),
                                    new LiteralExpressionNode(null, LiteralKind.Null),
                                }),
                        });
                }
                else
                {
                    chain = new CallExpressionNode(
                        target: new MemberAccessExpressionNode(
                            @object: chain,
                            member: new IdentifierNode(method)),
                        arguments: new ExpressionNode[]
                        {
                            new CallExpressionNode(
                                target: new MemberAccessExpressionNode(
                                    @object: new IdentifierExpressionNode(new IdentifierNode("okhttp3.RequestBody")),
                                    member: new IdentifierNode("create")),
                                arguments: new ExpressionNode[]
                                {
                                    new CallExpressionNode(
                                        target: new IdentifierExpressionNode(new IdentifierNode("new byte[0]")),
                                        arguments: ImmutableList<ExpressionNode>.Empty),
                                    new LiteralExpressionNode(null, LiteralKind.Null),
                                }),
                        });
                }
                break;
        }

        chain = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: chain,
                member: new IdentifierNode("build")),
            arguments: ImmutableList<ExpressionNode>.Empty);

        return chain;
    }

    private static bool IsStringType(IrType t) => t switch
    {
        IrPrimitiveType p => p.PrimitiveKind == ValIO.Core.IR.Enums.IrPrimitiveKind.String,
        IrNullableType n => IsStringType(n.InnerType),
        IrOptionalType o => IsStringType(o.InnerType),
        _ => false,
    };

    private static string NormalizePackage(string pkg) => pkg.Trim('.').Replace('/', '.');

    private static string BuildClassName(string clientName)
    {
        if (string.IsNullOrWhiteSpace(clientName)) return "ApiClient";
        return clientName.EndsWith("Api", StringComparison.OrdinalIgnoreCase)
            ? clientName
            : clientName + "Api";
    }

    private string ExpressionToString(ExpressionNode node)
    {
        // Limited inline visitor used only when generating try-with-resources source line.
        // Mirrors a subset of JavaAstRenderer behaviour.
        return node switch
        {
            CallExpressionNode call => $"{(call.IsNewExpression ? "new " : "")}{ExpressionToString(call.Target)}({string.Join(", ", call.Arguments.Select(ExpressionToString))})",
            MemberAccessExpressionNode m => $"{ExpressionToString(m.Object)}.{m.Member.Name}",
            IdentifierExpressionNode id => id.Identifier.Name,
            LiteralExpressionNode lit when lit.LiteralKind == LiteralKind.String => $"\"{lit.Value}\"",
            LiteralExpressionNode lit when lit.LiteralKind == LiteralKind.Null   => "null",
            LiteralExpressionNode lit => lit.Value?.ToString() ?? "null",
            _ => node.ToString() ?? string.Empty,
        };
    }
}
