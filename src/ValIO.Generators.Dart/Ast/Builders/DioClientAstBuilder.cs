using ValIO.Core.Ast.Builders;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Dart.Ast.Nodes;

namespace ValIO.Generators.Dart.Ast.Builders;

/// <summary>
/// Builds a Dart class per <see cref="IrClient"/> with one async method
/// per operation that delegates to a <c>Dio</c> instance.
/// </summary>
public sealed class DioClientAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly DartIrTypeMapper _typeMapper;

    public DioClientAstBuilder(INamingConverter namingConverter, DartIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

    public DartFileNode BuildFile(IrClient client)
    {
        var serviceName = _namingConverter.ToPascalCase(client.Name) + "Service";
        var dioField = new DartFieldNode(
            new IdentifierNode("_dio"),
            new SimpleTypeNode(new IdentifierNode("Dio")),
            isNullable: false,
            isFinal: true);

        var ctor = new DartMethodNode(
            new IdentifierNode(serviceName),
            returnType: null,
            parameters: new[]
            {
                new DartParameterNode(
                    new IdentifierNode("this._dio"),
                    new SimpleTypeNode(new IdentifierNode("Dio")),
                    isRequired: true,
                    isNamed: false),
            },
            isAsync: false,
            isStatic: false,
            isConstructor: true);

        var methods = new List<DartMethodNode> { ctor };
        foreach (var op in client.Operations)
            methods.Add(BuildMethod(op));

        var cls = new DartClassDeclarationNode(
            new IdentifierNode(serviceName),
            fields: new[] { dioField },
            methods: methods);

        var imports = new List<DartImportNode> { new("package:dio/dio.dart") };
        return new DartFileNode(declarations: new[] { cls }, imports: imports);
    }

    /// <summary>Builds a typed Dart operation declaration and its executable body.</summary>
    /// <param name="operation">The contract to lower.</param>
    /// <returns>The generated service method.</returns>
    private DartMethodNode BuildMethod(IrOperation operation)
    {
        var methodName = _namingConverter.ToCamelCase(operation.MethodName);
        var safeMethodName = DartReservedNames.Avoid(methodName);

        var parameters = new List<DartParameterNode>();
        foreach (var p in operation.PathParameters)
            parameters.Add(Param(p));
        foreach (var p in operation.QueryParameters)
            parameters.Add(Param(p));
        foreach (var p in operation.HeaderParameters)
            parameters.Add(Param(p));
        if (operation.RequestBody != null)
        {
            parameters.Add(new DartParameterNode(
                new IdentifierNode("body"),
                _typeMapper.MapType(operation.RequestBody.Type),
                isRequired: operation.RequestBody.IsRequired,
                isNamed: true));
        }

        var returnType = ResolveReturnType(operation);
        var hasResponse = operation.Responses.Any(response => response.Type is not null &&
            response.StatusCode.StartsWith("2", StringComparison.Ordinal));
        var body = BuildBody(operation, hasResponse);

        return new DartMethodNode(
            new IdentifierNode(safeMethodName),
            returnType: returnType,
            parameters: parameters,
            body: body,
            isAsync: true);
    }

    /// <summary>Declares a formal parameter using the same name as its references in the body.</summary>
    /// <param name="p">The operation parameter.</param>
    /// <returns>The typed Dart parameter.</returns>
    private DartParameterNode Param(IrParameter p)
    {
        var safe = ParameterName(p);
        return new DartParameterNode(
            new IdentifierNode(safe),
            _typeMapper.MapType(p.Type),
            isRequired: p.IsRequired,
            isNamed: true);
    }

    private TypeNode ResolveReturnType(IrOperation op)
    {
        // 200/2xx response with a body type if any.
        foreach (var resp in op.Responses)
        {
            if (resp.Type != null && resp.StatusCode.StartsWith("2", StringComparison.Ordinal))
            {
                var inner = _typeMapper.MapType(resp.Type);
                return new GenericTypeNode(new IdentifierNode("Future"), new[] { inner });
            }
        }
        return new GenericTypeNode(new IdentifierNode("Future"),
            new TypeNode[] { new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true) });
    }

    /// <summary>Builds the Dio invocation and its result handling from the operation.</summary>
    /// <param name="operation">The HTTP operation.</param>
    /// <param name="hasResponse">Whether the projected success result has a body.</param>
    /// <returns>The typed method body.</returns>
    private BlockNode BuildBody(IrOperation operation, bool hasResponse)
    {
        var statements = new List<StatementNode>();
        var arguments = new List<ExpressionNode> { OpenApiPathExpressionBuilder.Build(operation, parameter => Ref(ParameterName(parameter))) };
        var method = operation.HttpMethod.ToLowerInvariant();

        var member = method;
        var acceptsBody = method is "post" or "put" or "patch";
        if (!acceptsBody && method is not ("get" or "delete" or "head"))
        {
            member = "request";
            acceptsBody = method != "options";
            var options = new CallExpressionNode(Ref("Options"),
                [new DartNamedArgumentNode(new("method"), Text(operation.HttpMethod.ToUpperInvariant()))]);
            arguments.Add(new DartNamedArgumentNode(new("options"), options));
        }
        if (acceptsBody && operation.RequestBody is not null)
            arguments.Add(new DartNamedArgumentNode(new("data"), Ref("body")));

        if (operation.QueryParameters.Count > 0)
        {
            var entries = operation.QueryParameters.Select(parameter =>
                ((ExpressionNode)Text(parameter.OriginalName), (ExpressionNode)Ref(ParameterName(parameter)))).ToArray();
            var query = new DartMapLiteralNode(Type("String"), Type("dynamic"), entries);
            statements.Add(new VariableDeclarationStatementNode(new("query"), initializer: query));
            arguments.Add(new DartNamedArgumentNode(new("queryParameters"), Ref("query")));
        }

        var target = new MemberAccessExpressionNode(Ref("_dio"), new(member));
        var awaitResponse = new AwaitExpressionNode(new CallExpressionNode(target, arguments, genericTypeArgument: Type("dynamic")));
        if (hasResponse)
        {
            statements.Add(new VariableDeclarationStatementNode(new("response"), initializer: awaitResponse));
            statements.Add(new ReturnStatementNode(new MemberAccessExpressionNode(Ref("response"), new("data"))));
        }
        else
        {
            statements.Add(new ExpressionStatementNode(awaitResponse));
        }

        return new BlockNode(statements);
    }

    /// <summary>Gets the same Dart identifier for a declaration and its references.</summary>
    /// <param name="parameter">The operation parameter.</param>
    /// <returns>The normalized identifier.</returns>
    private string ParameterName(IrParameter parameter) =>
        DartReservedNames.Avoid(_namingConverter.ToCamelCase(parameter.Name));

    /// <summary>Creates an identifier reference.</summary>
    /// <param name="name">The name in scope.</param>
    /// <returns>The identifier expression.</returns>
    private static IdentifierExpressionNode Ref(string name) => new(new IdentifierNode(name));

    /// <summary>Captures a wire string for Dart literal escaping.</summary>
    /// <param name="value">The original wire value.</param>
    /// <returns>The string literal.</returns>
    private static LiteralExpressionNode Text(string value) => new(value, LiteralKind.String);

    /// <summary>References a Dart built-in type by name.</summary>
    /// <param name="name">The type name.</param>
    /// <returns>The type reference.</returns>
    private static SimpleTypeNode Type(string name) => new(new(name));
}
