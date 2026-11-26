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
using ValIO.Generators.Swift.Ast.Nodes;

namespace ValIO.Generators.Swift.Ast.Builders;

/// <summary>
/// Builds a Swift <c>public final class</c> wrapping URLSession + async/await
/// for one <see cref="IrClient"/>.
/// </summary>
public sealed class URLSessionClientAstBuilder(INamingConverter namingConverter, SwiftIrTypeMapper typeMapper)
{

    public SwiftFileNode BuildFile(IrClient client)
    {
        var className = ToClassName(client.Name);
        var importScan = new SwiftTypeImportScanner.Result { };
        importScan.StandardImports.Add("Foundation");

        var storedProps = new List<SwiftPropertyNode>
        {
            new SwiftPropertyNode(
                new IdentifierNode("session"),
                new SimpleTypeNode(new IdentifierNode("URLSession"), isPrimitive: true),
                accessModifier: "private",
                isConstant: true),
            new SwiftPropertyNode(
                new IdentifierNode("baseURL"),
                new SimpleTypeNode(new IdentifierNode("URL"), isPrimitive: true),
                accessModifier: "private",
                isConstant: true),
            new SwiftPropertyNode(
                new IdentifierNode("decoder"),
                new SimpleTypeNode(new IdentifierNode("JSONDecoder"), isPrimitive: true),
                accessModifier: "private",
                isConstant: true),
            new SwiftPropertyNode(
                new IdentifierNode("encoder"),
                new SimpleTypeNode(new IdentifierNode("JSONEncoder"), isPrimitive: true),
                accessModifier: "private",
                isConstant: true),
        };

        var members = new List<DeclarationNode>
        {
            BuildInitializer(),
        };

        foreach (var op in client.Operations)
        {
            members.Add(BuildOperationMethod(op, importScan));
        }

        var classNode = new SwiftClassDeclarationNode(
            new IdentifierNode(className),
            properties: storedProps,
            members: members,
            conformances: null,
            accessModifier: "public",
            isFinal: true,
            attributes: null,
            documentation: new CommentNode($"HTTP client for {client.Name}.", CommentKind.MultiLine));

        var imports = importScan.StandardImports
            .Select(name => new SwiftImportNode(name))
            .ToList();

        return new SwiftFileNode(declarations: [classNode], imports: imports);
    }

    private static SwiftFunctionDeclarationNode BuildInitializer()
    {
        var parameters = new List<SwiftParameterNode>
        {
            new SwiftParameterNode(
                new IdentifierNode("session"),
                new SimpleTypeNode(new IdentifierNode("URLSession"), isPrimitive: true),
                defaultValue: new SwiftImplicitMemberNode(new("shared"))),
            new SwiftParameterNode(
                new IdentifierNode("baseURL"),
                new SimpleTypeNode(new IdentifierNode("URL"), isPrimitive: true)),
            new SwiftParameterNode(
                new IdentifierNode("decoder"),
                new SimpleTypeNode(new IdentifierNode("JSONDecoder"), isPrimitive: true),
                defaultValue: Call(Ref("JSONDecoder"))),
            new SwiftParameterNode(
                new IdentifierNode("encoder"),
                new SimpleTypeNode(new IdentifierNode("JSONEncoder"), isPrimitive: true),
                defaultValue: Call(Ref("JSONEncoder"))),
        };

        var body = new BlockNode(parameters.Select(parameter =>
            (StatementNode)Assign(Member(Ref("self"), parameter.Name.Name), Ref(parameter.Name.Name))).ToArray());

        return new SwiftFunctionDeclarationNode(
            new IdentifierNode("init"),
            parameters,
            returnType: null,
            body: body,
            isAsync: false,
            isThrowing: false,
            accessModifier: "public",
            isStatic: false,
            attributes: null,
            documentation: null,
            isInitializer: true);
    }

    private SwiftFunctionDeclarationNode BuildOperationMethod(
        IrOperation op,
        SwiftTypeImportScanner.Result importScan)
    {
        var methodName = SwiftReservedNames.Avoid(namingConverter.ToCamelCase(op.MethodName));
        var parameters = new List<SwiftParameterNode>();

        foreach (var p in op.PathParameters)
        {
            SwiftTypeImportScanner.Walk(p.Type, importScan);
            parameters.Add(new SwiftParameterNode(
                new IdentifierNode(SwiftReservedNames.Avoid(namingConverter.ToCamelCase(p.Name))),
                typeMapper.MapType(p.Type)));
        }

        if (op.RequestBody != null)
        {
            SwiftTypeImportScanner.Walk(op.RequestBody.Type, importScan);
            parameters.Add(new SwiftParameterNode(
                new IdentifierNode("body"),
                typeMapper.MapType(op.RequestBody.Type)));
        }

        foreach (var p in op.QueryParameters)
        {
            SwiftTypeImportScanner.Walk(p.Type, importScan);
            var mapped = typeMapper.MapType(p.Type);
            ExpressionNode? defaultValue = null;
            if (!p.IsRequired)
            {
                mapped = mapped is NullableTypeNode ? mapped : new NullableTypeNode(mapped);
                defaultValue = Nil();
            }
            parameters.Add(new SwiftParameterNode(
                new IdentifierNode(SwiftReservedNames.Avoid(namingConverter.ToCamelCase(p.Name))),
                mapped,
                defaultValue));
        }

        // Pick first 2xx response as success.
        var successResponse = op.Responses.FirstOrDefault(r => r.StatusCode.StartsWith("2", StringComparison.Ordinal));
        TypeNode? returnType = null;
        bool isVoidReturn = successResponse?.Type == null;
        bool isBinaryReturn = false;
        if (successResponse?.Type != null)
        {
            SwiftTypeImportScanner.Walk(successResponse.Type, importScan);
            returnType = typeMapper.MapType(successResponse.Type);
            isBinaryReturn = successResponse.Type is IrPrimitiveType
            {
                PrimitiveKind: IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File,
            };
        }

        var body = BuildMethodBody(op, returnType, isVoidReturn, isBinaryReturn);

        return new SwiftFunctionDeclarationNode(
            new IdentifierNode(methodName),
            parameters,
            returnType,
            body,
            isAsync: true,
            isThrowing: true,
            accessModifier: "public",
            isStatic: false,
            attributes: null,
            documentation: BuildOperationDocstring(op),
            isInitializer: false);
    }

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

    /// <summary>Builds URL construction, request execution and typed response decoding.</summary>
    /// <param name="op">The operation and its ordered wire parameters.</param>
    /// <param name="returnType">The mapped response type, when declared.</param>
    /// <param name="isVoidReturn">Whether the method returns no body.</param>
    /// <param name="isBinaryReturn">Whether the method returns raw data.</param>
    /// <returns>The operation's executable Swift block.</returns>
    private BlockNode BuildMethodBody(IrOperation op, TypeNode? returnType, bool isVoidReturn, bool isBinaryReturn)
    {
        var route = OpenApiPathExpressionBuilder.Build(op, parameter => Ref(ParameterName(parameter)));
        var path = route.Parts.ToList();
        if (path.Count > 0 && path[0] is LiteralExpressionNode { Value: string leading })
            path[0] = Text(leading.TrimStart('/'));

        var appendPath = Call(Member(Ref("baseURL"), "appendingPathComponent"), new InterpolatedStringExpressionNode(path));
        var createUrl = Call(Ref("URLComponents"), Argument("url", appendPath),
            Argument("resolvingAgainstBaseURL", new LiteralExpressionNode(false, LiteralKind.Boolean)));
        var statements = new List<StatementNode> { Bind("components", createUrl, mutable: op.QueryParameters.Count > 0) };
        var components = Ref("components");
        if (op.QueryParameters.Count > 0)
        {
            var queryItems = Ref("queryItems");
            statements.Add(Bind("queryItems", new SwiftArrayExpressionNode([]), mutable: true, type: new ArrayTypeNode(Type("URLQueryItem"))));
            foreach (var parameter in op.QueryParameters)
            {
                var name = ParameterName(parameter);
                var value = Ref(name);
                var item = Call(Ref("URLQueryItem"), Argument("name", Text(parameter.OriginalName)),
                    Argument("value", new InterpolatedStringExpressionNode([value])));
                var add = new ExpressionStatementNode(Call(Member(queryItems, "append"), item));
                statements.Add(parameter.IsRequired ? add :
                    new IfStatementNode(new SwiftOptionalBindingNode(new(name), value), new BlockNode([add])));
            }

            var whenNotEmpty = new ConditionalExpressionNode(Member(queryItems, "isEmpty"), Nil(), queryItems);
            statements.Add(Assign(Member(components, "queryItems", optional: true), whenNotEmpty));
        }

        var urlBinding = new SwiftOptionalBindingNode(new("url"), Member(components, "url", optional: true));
        statements.Add(new SwiftGuardNode([urlBinding], UrlError("badURL")));
        var request = Ref("request");
        statements.Add(Bind("request", Call(Ref("URLRequest"), Argument("url", Ref("url"))), mutable: true));
        statements.Add(Assign(Member(request, "httpMethod"), Text(op.HttpMethod.ToUpperInvariant())));

        if (op.RequestBody is { } body)
        {
            var contentType = body.IsJson ? "application/json" : "application/octet-stream";
            var header = Call(Member(request, "setValue"), Text(contentType), Argument("forHTTPHeaderField", Text("Content-Type")));
            statements.Add(new ExpressionStatementNode(header));
            var encode = Call(Member(Ref("encoder"), "encode"), Ref("body"));
            statements.Add(Assign(Member(request, "httpBody"), new SwiftTryExpressionNode(encode)));
        }

        var send = Call(Member(Ref("session"), "data"), Argument("for", request));
        var receive = new SwiftTryExpressionNode(new AwaitExpressionNode(send));
        statements.Add(new SwiftBindingNode([new("data"), new("response")], receive, false, null));
        var cast = new CastExpressionNode(Type("HTTPURLResponse"), Ref("response"), useAsOperator: true);
        var httpResponse = new SwiftOptionalBindingNode(new("httpResponse"), cast);
        var status = Member(Ref("httpResponse"), "statusCode");
        var minimum = new BinaryExpressionNode(status, BinaryOperator.GreaterThanOrEqual, new LiteralExpressionNode(200, LiteralKind.Number));
        var maximum = new BinaryExpressionNode(status, BinaryOperator.LessThan, new LiteralExpressionNode(300, LiteralKind.Number));
        statements.Add(new SwiftGuardNode([httpResponse, new BinaryExpressionNode(minimum, BinaryOperator.LogicalAnd, maximum)],
            UrlError("badServerResponse")));

        var data = Ref("data");
        if (isVoidReturn)
        {
            statements.Add(Assign(Ref("_"), data));
        }
        else if (isBinaryReturn)
        {
            statements.Add(new ReturnStatementNode(data));
        }
        else if (returnType is not null)
        {
            var decode = Call(Member(Ref("decoder"), "decode"), new SwiftMetatypeExpressionNode(returnType), Argument("from", data));
            statements.Add(new ReturnStatementNode(new SwiftTryExpressionNode(decode)));
        }

        return new BlockNode(statements);
    }

    /// <summary>Creates an exit block for an invalid transport outcome.</summary>
    /// <param name="code">The URLError case to throw.</param>
    /// <returns>A block containing one throw statement.</returns>
    private static BlockNode UrlError(string code) =>
        new([new ThrowStatementNode(Call(Ref("URLError"), new SwiftImplicitMemberNode(new(code))))]);

    /// <summary>Gets the emitted identifier shared by a parameter and its references.</summary>
    /// <param name="parameter">The source parameter.</param>
    /// <returns>The Swift identifier.</returns>
    private string ParameterName(IrParameter parameter) => SwiftReservedNames.Avoid(namingConverter.ToCamelCase(parameter.Name));

    /// <summary>Creates a local binding with the requested mutability.</summary>
    /// <param name="name">The name of the declared symbol.</param>
    /// <param name="value">The initial value.</param>
    /// <param name="mutable">Whether the value is changed later in the method.</param>
    /// <param name="type">The explicit type when inference is insufficient.</param>
    /// <returns>The local declaration.</returns>
    private static SwiftBindingNode Bind(string name, ExpressionNode value, bool mutable, TypeNode? type = null) =>
        new([new(name)], value, mutable, type);

    /// <summary>Creates an assignment statement.</summary>
    /// <param name="target">The writable value.</param>
    /// <param name="value">The assigned value.</param>
    /// <returns>The assignment statement.</returns>
    private static ExpressionStatementNode Assign(ExpressionNode target, ExpressionNode value) =>
        new(new BinaryExpressionNode(target, BinaryOperator.Assign, value));

    /// <summary>Attaches a Swift argument label to a call operand.</summary>
    /// <param name="label">The caller-facing parameter name.</param>
    /// <param name="value">The argument expression.</param>
    /// <returns>The labeled argument.</returns>
    private static BinaryExpressionNode Argument(string label, ExpressionNode value) =>
        new(Ref(label), BinaryOperator.Assign, value, isNamedArgument: true);

    /// <summary>References a Swift identifier.</summary>
    /// <param name="name">The symbol name.</param>
    /// <returns>The reference expression.</returns>
    private static IdentifierExpressionNode Ref(string name) => new(new IdentifierNode(name));

    /// <summary>Selects a member of a Swift expression.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="name">The selected member.</param>
    /// <param name="optional">Whether the receiver uses optional chaining.</param>
    /// <returns>The member access expression.</returns>
    private static MemberAccessExpressionNode Member(ExpressionNode receiver, string name, bool optional = false) =>
        new(receiver, new(name), useOptionalChaining: optional);

    /// <summary>Invokes a Swift function or constructor.</summary>
    /// <param name="target">The callable expression.</param>
    /// <param name="arguments">The arguments in source order.</param>
    /// <returns>The call expression.</returns>
    private static CallExpressionNode Call(ExpressionNode target, params ExpressionNode[] arguments) => new(target, arguments);

    /// <summary>Captures source text so it will be escaped by the renderer.</summary>
    /// <param name="value">The input text.</param>
    /// <returns>The literal expression.</returns>
    private static LiteralExpressionNode Text(string value) => new(value, LiteralKind.String);

    /// <summary>Represents a Swift nil literal.</summary>
    /// <returns>The nil literal expression.</returns>
    private static LiteralExpressionNode Nil() => new(null, LiteralKind.Null);

    /// <summary>References a named Swift type.</summary>
    /// <param name="name">The type's identifier.</param>
    /// <returns>The type reference.</returns>
    private static SimpleTypeNode Type(string name) => new(new(name));

    private string ToClassName(string clientName)
    {
        var pascal = namingConverter.ToPascalCase(clientName);
        return pascal.EndsWith("Api", StringComparison.Ordinal) ? pascal : pascal + "Api";
    }
}
