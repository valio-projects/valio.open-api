using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.FSharp.Ast.Nodes;

namespace ValIO.Generators.FSharp.Ast.Builders;

/// <summary>
/// Builds an F# module of RestSharp-based functions (one let-binding per operation)
/// for one <see cref="IrClient"/>.
/// </summary>
public sealed class RestSharpClientAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly FSharpIrTypeMapper _typeMapper;

    public RestSharpClientAstBuilder(INamingConverter namingConverter, FSharpIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

    public FSharpFileNode BuildFile(IrClient client, string moduleName)
    {
        var functions = new List<DeclarationNode>(client.Operations.Count);
        foreach (var op in client.Operations)
        {
            functions.Add(BuildFunction(op));
        }

        return new FSharpFileNode(
            moduleName: moduleName,
            declarations: functions,
            opens: ["RestSharp", "System"]);
    }

    private FSharpFunctionNode BuildFunction(IrOperation op)
    {
        var methodName = FSharpReservedNames.Avoid(_namingConverter.ToCamelCase(op.MethodName));
        var parameters = new List<FSharpFunctionNode.Parameter>
        {
            // First parameter is always the RestSharp client instance.
            new("client", new SimpleTypeNode(new IdentifierNode("RestClient"))),
        };

        foreach (var p in op.PathParameters)
        {
            parameters.Add(new FSharpFunctionNode.Parameter(
                FSharpReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name)),
                _typeMapper.MapType(p.Type)));
        }

        if (op.RequestBody != null)
        {
            parameters.Add(new FSharpFunctionNode.Parameter(
                "body",
                _typeMapper.MapType(op.RequestBody.Type)));
        }

        foreach (var p in op.QueryParameters)
        {
            var mapped = _typeMapper.MapType(p.Type);
            if (!p.IsRequired && mapped is not NullableTypeNode)
                mapped = new NullableTypeNode(mapped);
            parameters.Add(new FSharpFunctionNode.Parameter(
                FSharpReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name)),
                mapped));
        }

        var successResponse = op.Responses.FirstOrDefault(
            r => r.StatusCode.StartsWith("2", StringComparison.Ordinal));
        TypeNode? returnType = null;
        if (successResponse?.Type != null)
            returnType = _typeMapper.MapType(successResponse.Type);

        var bodyLines = BuildFunctionBody(op);

        return new FSharpFunctionNode(
            new IdentifierNode(methodName),
            parameters,
            returnType,
            bodyLines,
            documentation: BuildDocstring(op));
    }

    private IReadOnlyList<string> BuildFunctionBody(IrOperation op)
    {
        var lines = new List<string>();
        var pathTemplate = op.Path;
        foreach (var p in op.PathParameters)
        {
            var camel = FSharpReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name));
            pathTemplate = pathTemplate.Replace("{" + p.Name + "}", "{" + camel + "}", StringComparison.Ordinal);
        }

        // RestSharp Method enum value (Method.Get, Method.Post, ...).
        var httpMethod = ToMethodCase(op.HttpMethod);
        lines.Add($"let req = RestRequest($\"{EscapeStringLiteral(pathTemplate)}\", Method.{httpMethod})");

        foreach (var p in op.QueryParameters)
        {
            var camel = FSharpReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name));
            var wireName = EscapeStringLiteral(p.Name);
            if (p.IsRequired)
            {
                lines.Add($"req.AddQueryParameter(\"{wireName}\", string {camel}) |> ignore");
            }
            else
            {
                lines.Add($"match {camel} with");
                lines.Add($"| Some v -> req.AddQueryParameter(\"{wireName}\", string v) |> ignore");
                lines.Add("| None -> ()");
            }
        }

        if (op.RequestBody != null)
        {
            lines.Add("req.AddJsonBody(body) |> ignore");
        }

        lines.Add("client.Execute(req)");
        return lines;
    }

    private static string ToMethodCase(string httpMethod) =>
        string.IsNullOrEmpty(httpMethod)
            ? "Get"
            : char.ToUpperInvariant(httpMethod[0]) + httpMethod.Substring(1).ToLowerInvariant();

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
        return new CommentNode(sb.ToString(), CommentKind.MultiLine);
    }

    private static string EscapeStringLiteral(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal);
}
