using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
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
public sealed class ReqwestClientAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly RustIrTypeMapper _typeMapper;

    public ReqwestClientAstBuilder(INamingConverter namingConverter, RustIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

    public RustFileNode BuildFile(IrClient client)
    {
        var functions = new List<DeclarationNode>(client.Operations.Count);
        var uses = new SortedSet<string>(StringComparer.Ordinal)
        {
            "reqwest::Client",
            "serde::{Deserialize, Serialize}",
        };

        foreach (var op in client.Operations)
        {
            functions.Add(BuildOperationFunction(op, uses));
        }

        return new RustFileNode(declarations: functions, uses: uses.ToImmutableList());
    }

    private RustFunctionNode BuildOperationFunction(IrOperation op, SortedSet<string> uses)
    {
        var methodName = RustReservedNames.Avoid(_namingConverter.ToSnakeCase(op.MethodName));
        var parameters = new List<RustParameter>
        {
            new("client", new SimpleTypeNode(new IdentifierNode("&Client"), isPrimitive: true)),
            new("base_url", new SimpleTypeNode(new IdentifierNode("&str"), isPrimitive: true)),
        };

        foreach (var p in op.PathParameters)
        {
            ScanForUses(p.Type, uses);
            parameters.Add(new RustParameter(
                RustReservedNames.Avoid(_namingConverter.ToSnakeCase(p.Name)),
                _typeMapper.MapType(p.Type)));
        }

        if (op.RequestBody != null)
        {
            ScanForUses(op.RequestBody.Type, uses);
            parameters.Add(new RustParameter(
                "body",
                _typeMapper.MapType(op.RequestBody.Type)));
        }

        foreach (var p in op.QueryParameters)
        {
            ScanForUses(p.Type, uses);
            var mapped = _typeMapper.MapType(p.Type);
            if (!p.IsRequired)
                mapped = EnsureOption(mapped);
            parameters.Add(new RustParameter(
                RustReservedNames.Avoid(_namingConverter.ToSnakeCase(p.Name)),
                mapped));
        }

        // Pick first 2xx response as success.
        var successResponse = op.Responses.FirstOrDefault(r => r.StatusCode.StartsWith("2", StringComparison.Ordinal));
        TypeNode? successType = null;
        bool isVoidReturn = successResponse?.Type == null;
        bool isBinaryReturn = false;
        if (successResponse?.Type != null)
        {
            ScanForUses(successResponse.Type, uses);
            successType = _typeMapper.MapType(successResponse.Type);
            isBinaryReturn = successResponse.Type is IrPrimitiveType
            {
                PrimitiveKind: IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File,
            };
        }

        // Wrap success in Result<T, reqwest::Error>.
        var unitType = new SimpleTypeNode(new IdentifierNode("()"), isPrimitive: true);
        var returnInner = successType ?? unitType;
        var returnType = new GenericTypeNode(
            new IdentifierNode("Result"),
            ImmutableList.Create<TypeNode>(
                returnInner,
                new SimpleTypeNode(new IdentifierNode("reqwest::Error"), isPrimitive: true)));

        var body = BuildBody(op, successType, isVoidReturn, isBinaryReturn);

        return new RustFunctionNode(
            new IdentifierNode(methodName),
            parameters,
            returnType,
            body,
            isAsync: true,
            visibility: "pub",
            documentation: BuildDocstring(op));
    }

    private IReadOnlyList<string> BuildBody(
        IrOperation op,
        TypeNode? successType,
        bool isVoidReturn,
        bool isBinaryReturn)
    {
        var lines = new List<string>();

        // Build the path by substituting path params with Rust format-string holes.
        var pathTemplate = op.Path.TrimStart('/');
        var formatArgs = new List<string>();
        foreach (var p in op.PathParameters)
        {
            var snake = RustReservedNames.Avoid(_namingConverter.ToSnakeCase(p.Name));
            pathTemplate = pathTemplate.Replace("{" + p.Name + "}", "{" + snake + "}", StringComparison.Ordinal);
            formatArgs.Add($"{snake} = {snake}");
        }

        var pathExpr = formatArgs.Count > 0
            ? $"format!(\"{{base_url}}/{pathTemplate}\", base_url = base_url, {string.Join(", ", formatArgs)})"
            : $"format!(\"{{base_url}}/{pathTemplate}\", base_url = base_url)";

        lines.Add($"let url = {pathExpr};");

        var method = op.HttpMethod.ToLowerInvariant() switch
        {
            "get" => "get",
            "post" => "post",
            "put" => "put",
            "delete" => "delete",
            "patch" => "patch",
            "head" => "head",
            _ => "request",
        };

        lines.Add($"let mut req = client.{method}(&url);");

        if (op.QueryParameters.Count > 0)
        {
            lines.Add("let mut query: Vec<(&str, String)> = Vec::new();");
            foreach (var p in op.QueryParameters)
            {
                var snake = RustReservedNames.Avoid(_namingConverter.ToSnakeCase(p.Name));
                var wireName = EscapeStringLiteral(p.Name);
                if (p.IsRequired)
                {
                    lines.Add($"query.push((\"{wireName}\", format!(\"{{}}\", {snake})));");
                }
                else
                {
                    lines.Add($"if let Some(v) = &{snake} {{ query.push((\"{wireName}\", format!(\"{{}}\", v))); }}");
                }
            }
            lines.Add("req = req.query(&query);");
        }

        if (op.RequestBody != null)
        {
            if (op.RequestBody.IsJson)
            {
                lines.Add("req = req.json(&body);");
            }
            else
            {
                // Treat anything else as JSON for the MVP fallback.
                lines.Add("req = req.json(&body);");
            }
        }

        lines.Add("let resp = req.send().await?.error_for_status()?;");

        if (isVoidReturn)
        {
            lines.Add("let _ = resp;");
            lines.Add("Ok(())");
        }
        else if (isBinaryReturn)
        {
            lines.Add("let bytes = resp.bytes().await?;");
            lines.Add("Ok(bytes.to_vec())");
        }
        else if (successType != null)
        {
            lines.Add("let parsed = resp.json().await?;");
            lines.Add("Ok(parsed)");
        }
        else
        {
            lines.Add("Ok(())");
        }

        return lines;
    }

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

    private static void ScanForUses(IrType irType, SortedSet<string> uses)
    {
        // Scan for chrono types in IR types.
        switch (irType)
        {
            case IrPrimitiveType p when p.PrimitiveKind is IrPrimitiveKind.Date
                                                       or IrPrimitiveKind.DateTime
                                                       or IrPrimitiveKind.Time:
                uses.Add("chrono");
                break;
            case IrArrayType a:
                ScanForUses(a.ElementType, uses);
                break;
            case IrDictionaryType d:
                ScanForUses(d.KeyType, uses);
                ScanForUses(d.ValueType, uses);
                break;
            case IrNullableType n:
                ScanForUses(n.InnerType, uses);
                break;
            case IrOptionalType o:
                ScanForUses(o.InnerType, uses);
                break;
        }
    }

    private static string EscapeStringLiteral(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal);
}
