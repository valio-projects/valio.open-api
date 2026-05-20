using ValIO.Core.Ast.Nodes;
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
            bodyLines: null,
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
        var bodyLines = BuildBody(operation);

        return new DartMethodNode(
            new IdentifierNode(safeMethodName),
            returnType: returnType,
            parameters: parameters,
            bodyLines: bodyLines,
            isAsync: true);
    }

    private DartParameterNode Param(IrParameter p)
    {
        var safe = DartReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name));
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

    private static List<string> BuildBody(IrOperation operation)
    {
        var lines = new List<string>();
        var pathExpr = BuildPathExpression(operation);

        // queryParameters map literal (named entry list).
        if (operation.QueryParameters.Count > 0)
        {
            lines.Add("final query = <String, dynamic>{");
            foreach (var qp in operation.QueryParameters)
                lines.Add($"  '{qp.Name}': {qp.Name},");
            lines.Add("};");
        }

        var queryArg = operation.QueryParameters.Count > 0 ? ", queryParameters: query" : string.Empty;
        var bodyArg = operation.RequestBody != null ? ", data: body" : string.Empty;
        var method = operation.HttpMethod.ToLowerInvariant();
        var dioCall = method switch
        {
            "get"     => $"_dio.get<dynamic>({pathExpr}{queryArg})",
            "post"    => $"_dio.post<dynamic>({pathExpr}{bodyArg}{queryArg})",
            "put"     => $"_dio.put<dynamic>({pathExpr}{bodyArg}{queryArg})",
            "patch"   => $"_dio.patch<dynamic>({pathExpr}{bodyArg}{queryArg})",
            "delete"  => $"_dio.delete<dynamic>({pathExpr}{queryArg})",
            "head"    => $"_dio.head<dynamic>({pathExpr}{queryArg})",
            "options" => $"_dio.request<dynamic>({pathExpr}, options: Options(method: 'OPTIONS'){queryArg})",
            _          => $"_dio.request<dynamic>({pathExpr}, options: Options(method: '{method.ToUpperInvariant()}'){bodyArg}{queryArg})",
        };

        lines.Add($"final response = await {dioCall};");
        lines.Add("return response.data;");
        return lines;
    }

    private static string BuildPathExpression(IrOperation operation)
    {
        // If there are path params, build a string interpolation literal:
        //   '/pets/${id}'
        // Dart string interpolation handles both single and braced forms.
        if (operation.PathParameters.Count == 0)
            return $"'{operation.Path}'";
        return $"'{operation.Path.Replace("{", "${", StringComparison.Ordinal)}'";
    }
}
