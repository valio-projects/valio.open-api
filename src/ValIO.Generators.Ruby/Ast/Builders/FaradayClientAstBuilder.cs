using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Ruby.Ast.Nodes;

namespace ValIO.Generators.Ruby.Ast.Builders;

/// <summary>
/// Builds a Ruby <c>class</c> named <c>{Tag}Service</c> wrapping a Faraday
/// connection for one <see cref="IrClient"/>. Each <see cref="IrOperation"/>
/// becomes a <c>def</c> with body lines that issue a Faraday request and
/// return the parsed response body.
/// </summary>
public sealed class FaradayClientAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly RubyIrTypeMapper _typeMapper;

    public FaradayClientAstBuilder(INamingConverter namingConverter, RubyIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

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

    private static RubyMethodNode BuildInitializer()
    {
        var parameters = new List<string> { "conn" };
        var body = new List<string> { "@conn = conn" };
        return new RubyMethodNode(
            new IdentifierNode("initialize"),
            parameters,
            body);
    }

    private RubyMethodNode BuildOperationMethod(IrOperation op)
    {
        var methodName = _namingConverter.ToSnakeCase(op.MethodName);
        var parameters = new List<string>();

        foreach (var p in op.PathParameters)
        {
            parameters.Add(_namingConverter.ToSnakeCase(p.Name));
        }

        if (op.RequestBody != null)
        {
            parameters.Add("body");
        }

        foreach (var p in op.QueryParameters)
        {
            var snake = _namingConverter.ToSnakeCase(p.Name);
            // All query params get a `nil` default to keep the call site flexible.
            parameters.Add($"{snake}: nil");
        }

        var body = BuildMethodBody(op);

        return new RubyMethodNode(
            new IdentifierNode(methodName),
            parameters: parameters,
            body: body,
            documentation: BuildOperationDocstring(op));
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

    private IReadOnlyList<string> BuildMethodBody(IrOperation op)
    {
        var lines = new List<string>();

        // Build the path by substituting path params using Ruby string interpolation.
        var pathTemplate = op.Path;
        foreach (var p in op.PathParameters)
        {
            var snake = _namingConverter.ToSnakeCase(p.Name);
            pathTemplate = pathTemplate.Replace("{" + p.Name + "}", "#{" + snake + "}", StringComparison.Ordinal);
        }

        var httpMethod = op.HttpMethod.ToLowerInvariant();
        var hasQuery = op.QueryParameters.Count > 0;
        var hasBody = op.RequestBody != null;

        // Use a block form so we can set query/body cleanly.
        lines.Add($"response = @conn.{httpMethod}(\"{EscapeDoubleQuoted(pathTemplate)}\") do |req|");

        if (hasQuery)
        {
            foreach (var p in op.QueryParameters)
            {
                var snake = _namingConverter.ToSnakeCase(p.Name);
                var wireName = EscapeSingleQuoted(p.Name);
                lines.Add($"  req.params['{wireName}'] = {snake} unless {snake}.nil?");
            }
        }

        if (hasBody)
        {
            if (op.RequestBody!.IsJson)
            {
                lines.Add("  req.headers['Content-Type'] = 'application/json'");
                lines.Add("  req.body = JSON.generate(body) unless body.nil?");
            }
            else
            {
                lines.Add("  req.body = body unless body.nil?");
            }
        }

        if (!hasQuery && !hasBody)
        {
            // Empty block: emit a no-op comment so the do/end is non-empty.
            lines.Add("  # no parameters");
        }

        lines.Add("end");
        lines.Add("response.body");

        return lines;
    }

    private string ToServiceClassName(string clientName)
    {
        var pascal = _namingConverter.ToPascalCase(clientName);
        return pascal.EndsWith("Service", StringComparison.Ordinal) ? pascal : pascal + "Service";
    }

    private static string EscapeDoubleQuoted(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string EscapeSingleQuoted(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("'", "\\'", StringComparison.Ordinal);
}
