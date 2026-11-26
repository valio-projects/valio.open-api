using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.FSharp.Ast.Nodes;

namespace ValIO.Generators.FSharp.Ast.Builders;

/// <summary>
/// Builds an F# module of RestSharp-based functions (one let-binding per operation)
/// for one <see cref="IrClient"/>.
/// </summary>
/// <param name="namingConverter">The operation identifier converter.</param>
/// <param name="typeMapper">The F# type mapper.</param>
public sealed class RestSharpClientAstBuilder(INamingConverter namingConverter, FSharpIrTypeMapper typeMapper)
{

    /// <summary>Builds asynchronous functions using the caller-owned RestClient pipeline.</summary>
    /// <param name="client">The operations grouped into the generated API module.</param>
    /// <param name="moduleName">The fully qualified output module.</param>
    /// <returns>A client module with explicit model imports and cancellation-aware operations.</returns>
    public FSharpFileNode BuildFile(IrClient client, string moduleName)
    {
        var functions = new List<DeclarationNode>(client.Operations.Count);
        foreach (var op in client.Operations)
        {
            functions.Add(BuildFunction(op));
        }

        var apiNamespaceEnd = moduleName.LastIndexOf(".Api.", StringComparison.Ordinal);
        var modelsNamespace = apiNamespaceEnd < 0 ? "Generated.Models" : moduleName[..apiNamespaceEnd] + ".Models";
        var types = client.Operations.SelectMany(operation => operation.PathParameters
                .Concat(operation.QueryParameters).Concat(operation.HeaderParameters).Select(parameter => parameter.Type)
                .Concat(operation.Responses.Where(response => response.Type != null).Select(response => response.Type!))
                .Concat(operation.RequestBody is { } request ? [request.Type] : []));
        var opens = FSharpModelImports.Collect(types, modelsNamespace, moduleName)
            .Concat(["RestSharp", "System", "System.Threading", "System.Threading.Tasks"]).ToArray();
        return new FSharpFileNode(
            moduleName: moduleName,
            declarations: functions,
            opens: opens);
    }

    /// <summary>Builds a task-returning operation with a cancellation token for the actual HTTP request.</summary>
    /// <param name="op">The operation to generate.</param>
    /// <returns>The F# function declaration.</returns>
    private FSharpFunctionNode BuildFunction(IrOperation op)
    {
        var methodName = FSharpReservedNames.Avoid(namingConverter.ToCamelCase(op.MethodName));
        var parameters = new List<FSharpFunctionNode.Parameter>
        {
            // First parameter is always the RestSharp client instance.
            new("client", new SimpleTypeNode(new IdentifierNode("RestClient"))),
        };

        foreach (var p in op.PathParameters)
        {
            parameters.Add(new FSharpFunctionNode.Parameter(
                FSharpReservedNames.Avoid(namingConverter.ToCamelCase(p.Name)),
                typeMapper.MapType(p.Type)));
        }

        if (op.RequestBody != null)
        {
            parameters.Add(new FSharpFunctionNode.Parameter(
                "body",
                typeMapper.MapType(op.RequestBody.Type)));
        }

        foreach (var p in op.QueryParameters.Concat(op.HeaderParameters))
        {
            var mapped = typeMapper.MapType(p.Type);
            if (!p.IsRequired && mapped is not NullableTypeNode)
                mapped = new NullableTypeNode(mapped);
            parameters.Add(new FSharpFunctionNode.Parameter(
                FSharpReservedNames.Avoid(namingConverter.ToCamelCase(p.Name)),
                mapped));
        }

        parameters.Add(new FSharpFunctionNode.Parameter("cancellationToken", new SimpleTypeNode(new IdentifierNode("CancellationToken"))));
        var successResponse = op.Responses.FirstOrDefault(response =>
            (int.TryParse(response.StatusCode, out var code) && code is >= 200 and < 300 || response.StatusCode == "2XX") && response.Type != null);
        var payload = successResponse?.Type == null
            ? new SimpleTypeNode(new IdentifierNode("unit"))
            : typeMapper.MapType(successResponse.Type);
        TypeNode returnType = new GenericTypeNode(new IdentifierNode("Task"), [payload]);

        var resultType = successResponse?.Type is null ? null : payload;
        var body = BuildFunctionBody(op, resultType);

        return new FSharpFunctionNode(
            new IdentifierNode(methodName),
            parameters,
            returnType,
            body,
            documentation: BuildDocstring(op));
    }

    /// <summary>Builds request construction and typed asynchronous response handling.</summary>
    /// <param name="op">The operation whose parameters retain their original wire names.</param>
    /// <param name="responseType">The selected success type, or null for bodyless operations.</param>
    /// <returns>The AST for the RestSharp computation.</returns>
    private BlockNode BuildFunctionBody(IrOperation op, TypeNode? responseType)
    {
        var cancellation = Ref("cancellationToken");
        var verifyCancellation = new ExpressionStatementNode(Call(Member(cancellation, "ThrowIfCancellationRequested")));
        var request = Ref("req");
        var route = Call(Ref("RestRequest"), Text(op.Path), Member(Ref("Method"), ToMethodCase(op.HttpMethod)));
        var statements = new List<StatementNode>
        {
            verifyCancellation,
            new VariableDeclarationStatementNode(new("req"), initializer: route),
        };
        foreach (var parameter in op.PathParameters)
            statements.Add(WriteParameter("AddUrlSegment", parameter, Ref(ParameterName(parameter))));

        foreach (var parameter in op.QueryParameters)
            statements.Add(BindParameter("AddQueryParameter", parameter));

        foreach (var parameter in op.HeaderParameters)
            statements.Add(BindParameter("AddHeader", parameter));

        if (op.RequestBody is not null)
            statements.Add(Ignore(Call(Member(request, "AddJsonBody"), Ref("body"))));

        var send = new CallExpressionNode(Member(Ref("client"), "ExecuteAsync"), [request, cancellation], genericTypeArgument: responseType);
        statements.Add(new FSharpAwaitBindingNode(new("response"), send));
        statements.Add(verifyCancellation);

        var response = Ref("response");
        var status = Member(response, "StatusCode");
        var message = new InterpolatedStringExpressionNode([
            Text("HTTP "), Apply("int", status), Text(": "), Member(response, "Content"),
        ]);
        var failure = Call(Ref("System.Net.Http.HttpRequestException"), message,
            Member(response, "ErrorException"), Call(Ref("Nullable"), status));
        var unsuccessful = new UnaryExpressionNode(UnaryOperator.LogicalNot, Member(response, "IsSuccessful"));
        statements.Add(new IfStatementNode(unsuccessful, new BlockNode([new ThrowStatementNode(failure)])));

        if (responseType is null)
        {
            statements.Add(new ReturnStatementNode());
        }
        else
        {
            var data = Member(response, "Data");
            var isNull = Apply("isNull", Apply("box", data));
            var invalidData = Call(Ref("System.IO.InvalidDataException"),
                Text("The successful HTTP response did not contain a valid payload."));
            statements.Add(new IfStatementNode(isNull, new BlockNode([new ThrowStatementNode(invalidData)])));
            statements.Add(new ReturnStatementNode(data));
        }

        var task = new FSharpTaskExpressionNode(new BlockNode(statements));
        return new BlockNode([new ExpressionStatementNode(task)]);
    }

    /// <summary>Writes a required parameter or matches an optional parameter before writing.</summary>
    /// <param name="method">The request method that receives the wire value.</param>
    /// <param name="parameter">The declared header or query parameter.</param>
    /// <returns>The write or option match statement.</returns>
    private StatementNode BindParameter(string method, IrParameter parameter)
    {
        var argument = Ref(ParameterName(parameter));
        return parameter.IsRequired ? WriteParameter(method, parameter, argument) :
            new FSharpMatchOptionNode(argument, new("v"), WriteParameter(method, parameter, Ref("v")));
    }

    /// <summary>Passes the original wire name and stringified value to the request.</summary>
    /// <param name="method">The request mutator.</param>
    /// <param name="parameter">The source parameter.</param>
    /// <param name="argument">The supplied required or unwrapped value.</param>
    /// <returns>A statement discarding the RestSharp fluent return.</returns>
    private static ExpressionStatementNode WriteParameter(string method, IrParameter parameter, ExpressionNode argument) =>
        Ignore(Call(Member(Ref("req"), method), Text(parameter.OriginalName), Apply("string", argument)));

    /// <summary>Produces a local F# identifier for a request argument.</summary>
    /// <param name="parameter">The source parameter.</param>
    /// <returns>The same name emitted in the declaration.</returns>
    private string ParameterName(IrParameter parameter) => FSharpReservedNames.Avoid(namingConverter.ToCamelCase(parameter.Name));

    /// <summary>References an identifier or qualified symbol.</summary>
    /// <param name="name">The identifier in scope.</param>
    /// <returns>The symbol reference.</returns>
    private static IdentifierExpressionNode Ref(string name) => new(new IdentifierNode(name));

    /// <summary>Selects a member of an F# value.</summary>
    /// <param name="target">The receiver.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The member access expression.</returns>
    private static MemberAccessExpressionNode Member(ExpressionNode target, string name) => new(target, new(name));

    /// <summary>Invokes a method or constructor using a tuple of arguments.</summary>
    /// <param name="target">The method or constructor reference.</param>
    /// <param name="arguments">The positional arguments.</param>
    /// <returns>The call expression.</returns>
    private static CallExpressionNode Call(ExpressionNode target, params ExpressionNode[] arguments) => new(target, arguments);

    /// <summary>Applies an F# function to its argument.</summary>
    /// <param name="function">The name of the function.</param>
    /// <param name="argument">The input expression.</param>
    /// <returns>The application expression.</returns>
    private static FSharpApplicationNode Apply(string function, ExpressionNode argument) => new(Ref(function), argument);

    /// <summary>Discards a fluent method's return value.</summary>
    /// <param name="value">The expression that performs the mutation.</param>
    /// <returns>A statement that evaluates and discards the value.</returns>
    private static ExpressionStatementNode Ignore(ExpressionNode value) =>
        new(new BinaryExpressionNode(value, BinaryOperator.Pipe, Ref("ignore")));

    /// <summary>Captures string data so the renderer can escape it.</summary>
    /// <param name="value">The input text.</param>
    /// <returns>The literal expression.</returns>
    private static LiteralExpressionNode Text(string value) => new(value, LiteralKind.String);

    /// <summary>Maps an HTTP verb to the corresponding RestSharp enumeration member.</summary>
    /// <param name="httpMethod">The source HTTP verb.</param>
    /// <returns>The RestSharp method member name.</returns>
    private static string ToMethodCase(string httpMethod) =>
        string.IsNullOrEmpty(httpMethod)
            ? "Get"
            : char.ToUpperInvariant(httpMethod[0]) + httpMethod.Substring(1).ToLowerInvariant();

    /// <summary>Preserves operation documentation and describes the injected transport boundary.</summary>
    /// <param name="op">The source operation.</param>
    /// <returns>The generated F# XML documentation.</returns>
    private static CommentNode? BuildDocstring(IrOperation op)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(op.Summary))
            sb.Append(op.Summary!.Trim());
        if (!string.IsNullOrWhiteSpace(op.Description))
        {
            if (sb.Length > 0) sb.AppendLine().AppendLine();
            sb.Append(op.Description!.Trim());
        }
        if (sb.Length == 0)
            sb.Append($"{op.HttpMethod} {op.Path}");
        sb.AppendLine().Append("Uses the caller-owned RestClient and its interceptors/authenticator. The cancellation token reaches the HTTP transport.");
        return new CommentNode(sb.ToString(), CommentKind.MultiLine);
    }
}
