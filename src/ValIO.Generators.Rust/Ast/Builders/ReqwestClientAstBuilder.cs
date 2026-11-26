using System.Collections.Immutable;
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
using ValIO.Generators.Rust.Ast.Nodes;

namespace ValIO.Generators.Rust.Ast.Builders;

/// <summary>
/// Builds one Rust async fn per IR operation, using <c>reqwest::Client</c>.
/// Each function returns <c>Result&lt;ReturnType, reqwest::Error&gt;</c>.
/// </summary>
public sealed class ReqwestClientAstBuilder(INamingConverter namingConverter, RustIrTypeMapper typeMapper)
{

    /// <summary>Builds the API module and imports for its method signatures.</summary>
    /// <param name="client">The OpenAPI operation group.</param>
    /// <param name="context">The document's declarations and stable module names.</param>
    public RustFileNode BuildFile(IrClient client, IrDocumentContext context)
    {
        var functions = client.Operations.Select(BuildOperationFunction).ToArray();
        var types = client.Operations.SelectMany(operation => operation.PathParameters.Concat(operation.QueryParameters)
            .Select(parameter => parameter.Type)
            .Concat(operation.Responses.Select(response => response.Type).OfType<IrType>())
            .Concat(operation.RequestBody is { } request ? [request.Type] : []));
        IReadOnlyList<ImportNode> uses = [new([new("Client")], "reqwest"), .. RustImports.Models(context, namingConverter, types)];
        return new RustFileNode(declarations: functions, uses: uses);
    }

    /// <summary>Builds the declared function and its request body for one HTTP operation.</summary>
    /// <param name="op">The source operation.</param>
    /// <returns>The typed function declaration.</returns>
    private RustFunctionNode BuildOperationFunction(IrOperation op)
    {
        var methodName = RustReservedNames.Avoid(namingConverter.ToSnakeCase(op.MethodName));
        var parameters = new List<RustParameter>
        {
            new("client", new RustReferenceTypeNode(new SimpleTypeNode(new IdentifierNode("Client")))),
            new("base_url", new RustReferenceTypeNode(new SimpleTypeNode(new IdentifierNode("str"), isPrimitive: true))),
        };

        foreach (var p in op.PathParameters)
        {
            parameters.Add(new RustParameter(
                RustReservedNames.Avoid(namingConverter.ToSnakeCase(p.Name)),
                typeMapper.MapType(p.Type)));
        }

        if (op.RequestBody != null)
        {
            parameters.Add(new RustParameter(
                "body",
                typeMapper.MapType(op.RequestBody.Type)));
        }

        foreach (var p in op.QueryParameters)
        {
            var mapped = typeMapper.MapType(p.Type);
            if (!p.IsRequired)
                mapped = EnsureOption(mapped);
            parameters.Add(new RustParameter(
                RustReservedNames.Avoid(namingConverter.ToSnakeCase(p.Name)),
                mapped));
        }

        // Pick first 2xx response as success.
        var successResponse = op.Responses.FirstOrDefault(r => r.StatusCode.StartsWith("2", StringComparison.Ordinal));
        TypeNode? successType = null;
        bool isVoidReturn = successResponse?.Type == null;
        bool isBinaryReturn = false;
        if (successResponse?.Type != null)
        {
            successType = typeMapper.MapType(successResponse.Type);
            isBinaryReturn = successResponse.Type is IrPrimitiveType
            {
                PrimitiveKind: IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File,
            };
        }

        // Wrap success in Result<T, reqwest::Error>.
        var unitType = new RustUnitTypeNode();
        var returnInner = successType ?? unitType;
        var returnType = new GenericTypeNode(
            new IdentifierNode("Result"),
            ImmutableList.Create<TypeNode>(
                returnInner,
                new SimpleTypeNode(new IdentifierNode("reqwest::Error"), isPrimitive: true)));

        var (body, result) = BuildBody(op, isVoidReturn, isBinaryReturn);

        return new RustFunctionNode(
            new IdentifierNode(methodName),
            parameters,
            returnType,
            body,
            isAsync: true,
            visibility: "pub",
            documentation: BuildDocstring(op), tail: result);
    }

    /// <summary>Builds the typed request, response bindings, and function result.</summary>
    /// <param name="op">The source HTTP operation.</param>
    /// <param name="isVoidReturn">Whether the response has no payload.</param>
    /// <param name="isBinaryReturn">Whether the response body is buffered as bytes.</param>
    /// <returns>The body statements and the tail expression returned by the function.</returns>
    private (BlockNode Body, ExpressionNode Result) BuildBody(IrOperation op, bool isVoidReturn, bool isBinaryReturn)
    {
        var path = OpenApiPathExpressionBuilder.Build(op, parameter => Ref(ParameterName(parameter)));
        var parts = path.Parts.ToList();
        if (parts.Count > 0 && parts[0] is LiteralExpressionNode { Value: string leading })
            parts[0] = Text(leading.TrimStart('/'));

        var url = new InterpolatedStringExpressionNode([Ref("base_url"), Text("/"), .. parts]);
        var method = op.HttpMethod.ToLowerInvariant();
        var client = Ref("client");
        var urlReference = new RustBorrowNode(Ref("url"));
        var createRequest = method is "get" or "post" or "put" or "delete" or "patch" or "head"
            ? Call(Member(client, method), urlReference)
            : Call(Member(client, "request"), Member(Ref("reqwest::Method"), op.HttpMethod.ToUpperInvariant(), scope: true), urlReference);
        var req = Ref("req");
        var statements = new List<StatementNode>
        {
            new RustLetNode(new("url"), url, null, false),
            new RustLetNode(new("req"), createRequest, null, op.QueryParameters.Count > 0 || op.RequestBody is not null),
        };

        if (op.QueryParameters.Count > 0)
        {
            var query = Ref("query");
            statements.Add(new RustLetNode(new("query"), Call(Member(Ref("Vec"), "new", scope: true)), null, true));
            foreach (var parameter in op.QueryParameters)
            {
                var argument = Ref(ParameterName(parameter));
                var value = parameter.IsRequired ? argument : Ref("v");
                var formatted = new InterpolatedStringExpressionNode([value]);
                var entry = new RustTupleNode([Text(parameter.OriginalName), formatted]);
                var push = new ExpressionStatementNode(Call(Member(query, "push"), entry));
                statements.Add(parameter.IsRequired ? push :
                    new RustIfSomeNode(new("v"), new RustBorrowNode(argument), new BlockNode([push])));
            }
            statements.Add(Assign(req, Call(Member(req, "query"), new RustBorrowNode(query))));
        }

        if (op.RequestBody is not null)
            statements.Add(Assign(req, Call(Member(req, "json"), new RustBorrowNode(Ref("body")))));

        var sent = new RustTryNode(new AwaitExpressionNode(Call(Member(req, "send"))));
        var checkedStatus = new RustTryNode(Call(Member(sent, "error_for_status")));
        if (isVoidReturn)
        {
            statements.Add(new ExpressionStatementNode(checkedStatus));
            return (new BlockNode(statements), Call(Ref("Ok"), new RustTupleNode([])));
        }

        statements.Add(new RustLetNode(new("resp"), checkedStatus, null, false));
        var decode = new RustTryNode(new AwaitExpressionNode(Call(Member(Ref("resp"), isBinaryReturn ? "bytes" : "json"))));
        var resultName = isBinaryReturn ? "bytes" : "parsed";
        statements.Add(new RustLetNode(new(resultName), decode, null, false));
        var result = Ref(resultName);
        return (new BlockNode(statements), Call(Ref("Ok"), isBinaryReturn ? Call(Member(result, "to_vec")) : result));
    }

    /// <summary>Assigns a value to a mutable binding.</summary>
    /// <param name="target">The binding being changed.</param>
    /// <param name="value">The new value of the binding.</param>
    /// <returns>The assignment statement.</returns>
    private static ExpressionStatementNode Assign(ExpressionNode target, ExpressionNode value) =>
        new(new BinaryExpressionNode(target, BinaryOperator.Assign, value));

    /// <summary>Produces the emitted name shared by a formal parameter and its references.</summary>
    /// <param name="parameter">The parameter declared in OpenAPI.</param>
    /// <returns>The Rust identifier.</returns>
    private string ParameterName(IrParameter parameter) => RustReservedNames.Avoid(namingConverter.ToSnakeCase(parameter.Name));

    /// <summary>Creates a name reference.</summary>
    /// <param name="name">The Rust symbol name.</param>
    /// <returns>The reference expression.</returns>
    private static IdentifierExpressionNode Ref(string name) => new(new IdentifierNode(name));

    /// <summary>Selects an instance member or a scoped associated member.</summary>
    /// <param name="target">The receiver or type.</param>
    /// <param name="name">The selected identifier.</param>
    /// <param name="scope">Whether to use Rust's scope operator.</param>
    /// <returns>The member expression.</returns>
    private static MemberAccessExpressionNode Member(ExpressionNode target, string name, bool scope = false) =>
        new(target, new(name), useScopeOperator: scope);

    /// <summary>Creates a call to the selected expression.</summary>
    /// <param name="target">The function to call.</param>
    /// <param name="arguments">The positional arguments.</param>
    /// <returns>The invocation expression.</returns>
    private static CallExpressionNode Call(ExpressionNode target, params ExpressionNode[] arguments) => new(target, arguments);

    /// <summary>Records the wire value as a string literal node.</summary>
    /// <param name="value">The original wire text.</param>
    /// <returns>The literal expression.</returns>
    private static LiteralExpressionNode Text(string value) => new(value, LiteralKind.String);

    private static CommentNode? BuildDocstring(IrOperation op)
    {
        var text = !string.IsNullOrWhiteSpace(op.Summary)
            ? op.Summary!.Trim()
            : $"{op.HttpMethod} {op.Path}";
        if (!string.IsNullOrWhiteSpace(op.Description))
            text += "\n\n" + op.Description!.Trim();
        return new CommentNode(text, CommentKind.MultiLine);
    }

    private static TypeNode EnsureOption(TypeNode type) =>
        type is GenericTypeNode g && g.Name.Name == "Option"
            ? type
            : new GenericTypeNode(new IdentifierNode("Option"), ImmutableList.Create<TypeNode>(type));

}
