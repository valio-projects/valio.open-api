using ValIO.Core.Ast.Builders;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Ruby.Ast.Nodes;

namespace ValIO.Generators.Ruby.Ast.Builders;

/// <summary>
/// Builds a Ruby <c>class</c> named <c>{Tag}Service</c> wrapping a Faraday
/// connection for one <see cref="IrClient"/>. Each <see cref="IrOperation"/>
/// becomes a <c>def</c> that issues a Faraday request and
/// return the parsed response body.
/// </summary>
/// <param name="namingConverter">The naming policy for Ruby declarations.</param>
/// <param name="typeMapper">The mapper used by other Ruby artifacts.</param>
public sealed class FaradayClientAstBuilder(INamingConverter namingConverter, RubyIrTypeMapper typeMapper)
{

    public RubyFileNode BuildFile(IrClient client)
    {
        var className = ToServiceClassName(client.Name);
        var members = new List<DeclarationNode>
        {
            BuildInitializer(),
        };

        foreach (var op in client.Operations)
        {
            members.Add(BuildOperationMethod(op));
        }

        var classNode = new RubyClassNode(
            new IdentifierNode(className),
            parentClass: null,
            members: members,
            documentation: new CommentNode($"Faraday-based HTTP service for {client.Name}.", CommentKind.MultiLine));

        var requires = new List<string> { "faraday", "json" };
        return new RubyFileNode(classes: [classNode], requires: requires);
    }

    /// <summary>Builds the constructor that stores the supplied Faraday connection.</summary>
    /// <returns>The connection initializer.</returns>
    private static RubyMethodNode BuildInitializer() =>
        new(new("initialize"), [new(new("conn"), type: null)], new BlockNode([Assign(Ref("@conn"), Ref("conn"))]));

    /// <summary>Creates a Faraday service method for one HTTP operation.</summary>
    /// <param name="op">The source operation.</param>
    /// <returns>The method and its request statements.</returns>
    private RubyMethodNode BuildOperationMethod(IrOperation op)
    {
        var methodName = namingConverter.ToSnakeCase(op.MethodName);
        var parameters = new List<ParameterDeclarationNode>();

        foreach (var p in op.PathParameters)
        {
            parameters.Add(new(new(namingConverter.ToSnakeCase(p.Name)), type: null));
        }

        if (op.RequestBody != null)
        {
            parameters.Add(new(new("body"), type: null));
        }

        foreach (var p in op.QueryParameters)
        {
            var snake = namingConverter.ToSnakeCase(p.Name);
            // All query params get a `nil` default to keep the call site flexible.
            parameters.Add(new(new(snake), type: null, defaultValue: new LiteralExpressionNode(null, LiteralKind.Null)));
        }

        var body = BuildMethodBody(op);

        return new RubyMethodNode(
            new IdentifierNode(methodName),
            parameters: parameters,
            body: body,
            documentation: BuildOperationDocstring(op));
    }

    /// <summary>Captures public operation documentation without formatting source syntax.</summary>
    /// <param name="op">The original HTTP operation.</param>
    /// <returns>The summary and description, with the route as fallback.</returns>
    private static CommentNode BuildOperationDocstring(IrOperation op)
    {
        var parts = new[] { op.Summary, op.Description }.Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim()).ToArray();
        var description = parts.Length == 0 ? $"{op.HttpMethod} {op.Path}" : string.Join("\n\n", parts);
        return new CommentNode(description, CommentKind.MultiLine);
    }

    /// <summary>Builds the HTTP request block and the response body expression.</summary>
    /// <param name="op">The current HTTP operation.</param>
    /// <returns>The executable statements for the generated method.</returns>
    private BlockNode BuildMethodBody(IrOperation op)
    {
        var path = OpenApiPathExpressionBuilder.Build(op, parameter => Ref(namingConverter.ToSnakeCase(parameter.Name)));
        var verb = op.HttpMethod.ToLowerInvariant();
        var call = new CallExpressionNode(Member(Ref("@conn"), verb), [path]);
        var request = Ref("req");
        var block = new List<StatementNode>();

        foreach (var parameter in op.QueryParameters)
        {
            var value = Ref(namingConverter.ToSnakeCase(parameter.Name));
            var slot = new IndexAccessExpressionNode(Member(request, "params"), Text(parameter.OriginalName));
            block.Add(UnlessNil(value, Assign(slot, value)));
        }

        if (op.RequestBody is { } requestBody)
        {
            var body = Ref("body");
            ExpressionNode encoded = body;
            if (requestBody.IsJson)
            {
                var contentType = new IndexAccessExpressionNode(Member(request, "headers"), Text("Content-Type"));
                block.Add(Assign(contentType, Text("application/json")));
                encoded = new CallExpressionNode(Member(Ref("JSON"), "generate"), [body]);
            }
            block.Add(UnlessNil(body, Assign(Member(request, "body"), encoded)));
        }

        var send = new RubyCallWithBlockNode(call, new("req"), new BlockNode(block));
        return new BlockNode([
            new VariableDeclarationStatementNode(new("response"), initializer: send),
            new ExpressionStatementNode(Member(Ref("response"), "body")),
        ]);
    }

    /// <summary>Builds a Ruby assignment expression as a statement.</summary>
    /// <param name="target">The destination of the assignment.</param>
    /// <param name="value">The assigned value.</param>
    /// <returns>The assignment statement.</returns>
    private static ExpressionStatementNode Assign(ExpressionNode target, ExpressionNode value) =>
        new(new BinaryExpressionNode(target, BinaryOperator.Assign, value));

    /// <summary>Skips an optional write when its source value is nil.</summary>
    /// <param name="value">The value tested for nil.</param>
    /// <param name="statement">The write to execute for a present value.</param>
    /// <returns>A Ruby postfix condition.</returns>
    private static RubyUnlessStatementNode UnlessNil(ExpressionNode value, StatementNode statement) =>
        new(statement, Member(value, "nil?"));

    /// <summary>Selects a member of the given Ruby value.</summary>
    /// <param name="target">The receiver.</param>
    /// <param name="name">The selected member.</param>
    /// <returns>The member access expression.</returns>
    private static MemberAccessExpressionNode Member(ExpressionNode target, string name) => new(target, new(name));

    /// <summary>References a Ruby name in the current scope.</summary>
    /// <param name="name">The identifier or instance variable.</param>
    /// <returns>The reference expression.</returns>
    private static IdentifierExpressionNode Ref(string name) => new(new IdentifierNode(name));

    /// <summary>Captures wire text before it is escaped by the renderer.</summary>
    /// <param name="value">The original text.</param>
    /// <returns>A string literal.</returns>
    private static LiteralExpressionNode Text(string value) => new(value, LiteralKind.String);

    private string ToServiceClassName(string clientName)
    {
        var pascal = namingConverter.ToPascalCase(clientName);
        return pascal.EndsWith("Service", StringComparison.Ordinal) ? pascal : pascal + "Service";
    }

}
