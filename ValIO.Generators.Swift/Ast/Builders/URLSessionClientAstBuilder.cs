using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
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
public sealed class URLSessionClientAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly SwiftIrTypeMapper _typeMapper;

    public URLSessionClientAstBuilder(INamingConverter namingConverter, SwiftIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

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
                defaultValueLiteral: ".shared"),
            new SwiftParameterNode(
                new IdentifierNode("baseURL"),
                new SimpleTypeNode(new IdentifierNode("URL"), isPrimitive: true)),
            new SwiftParameterNode(
                new IdentifierNode("decoder"),
                new SimpleTypeNode(new IdentifierNode("JSONDecoder"), isPrimitive: true),
                defaultValueLiteral: "JSONDecoder()"),
            new SwiftParameterNode(
                new IdentifierNode("encoder"),
                new SimpleTypeNode(new IdentifierNode("JSONEncoder"), isPrimitive: true),
                defaultValueLiteral: "JSONEncoder()"),
        };

        var body = new List<string>
        {
            "self.session = session",
            "self.baseURL = baseURL",
            "self.decoder = decoder",
            "self.encoder = encoder",
        };

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
        var methodName = SwiftReservedNames.Avoid(_namingConverter.ToCamelCase(op.MethodName));
        var parameters = new List<SwiftParameterNode>();

        foreach (var p in op.PathParameters)
        {
            SwiftTypeImportScanner.Walk(p.Type, importScan);
            parameters.Add(new SwiftParameterNode(
                new IdentifierNode(SwiftReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name))),
                _typeMapper.MapType(p.Type)));
        }

        if (op.RequestBody != null)
        {
            SwiftTypeImportScanner.Walk(op.RequestBody.Type, importScan);
            parameters.Add(new SwiftParameterNode(
                new IdentifierNode("body"),
                _typeMapper.MapType(op.RequestBody.Type)));
        }

        foreach (var p in op.QueryParameters)
        {
            SwiftTypeImportScanner.Walk(p.Type, importScan);
            var mapped = _typeMapper.MapType(p.Type);
            string? defaultValue = null;
            if (!p.IsRequired)
            {
                mapped = mapped is NullableTypeNode ? mapped : new NullableTypeNode(mapped);
                defaultValue = "nil";
            }
            parameters.Add(new SwiftParameterNode(
                new IdentifierNode(SwiftReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name))),
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
            returnType = _typeMapper.MapType(successResponse.Type);
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

    private IReadOnlyList<string> BuildMethodBody(
        IrOperation op,
        TypeNode? returnType,
        bool isVoidReturn,
        bool isBinaryReturn)
    {
        var lines = new List<string>();

        // Build the path by substituting path params (using Swift string interpolation).
        // We escape the literal portion first, then inject \(...) placeholders so the
        // backslash that introduces Swift interpolation is not itself escaped.
        var rawPath = op.Path.TrimStart('/');
        var pathLiteral = EscapeStringLiteral(rawPath);
        foreach (var p in op.PathParameters)
        {
            var camel = SwiftReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name));
            pathLiteral = pathLiteral.Replace("{" + p.Name + "}", "\\(" + camel + ")", StringComparison.Ordinal);
        }
        lines.Add($"var components = URLComponents(url: baseURL.appendingPathComponent(\"{pathLiteral}\"), resolvingAgainstBaseURL: false)");

        // Query parameters via URLQueryItem.
        if (op.QueryParameters.Count > 0)
        {
            lines.Add("var queryItems: [URLQueryItem] = []");
            foreach (var p in op.QueryParameters)
            {
                var camel = SwiftReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name));
                var wireName = EscapeStringLiteral(p.Name);
                if (p.IsRequired)
                {
                    lines.Add($"queryItems.append(URLQueryItem(name: \"{wireName}\", value: \"\\({camel})\"))");
                }
                else
                {
                    lines.Add($"if let {camel} = {camel} {{ queryItems.append(URLQueryItem(name: \"{wireName}\", value: \"\\({camel})\")) }}");
                }
            }
            lines.Add("components?.queryItems = queryItems.isEmpty ? nil : queryItems");
        }

        lines.Add("guard let url = components?.url else { throw URLError(.badURL) }");
        lines.Add("var request = URLRequest(url: url)");

        var httpMethod = op.HttpMethod.ToUpperInvariant();
        lines.Add($"request.httpMethod = \"{httpMethod}\"");

        if (op.RequestBody != null)
        {
            if (op.RequestBody.IsJson)
            {
                lines.Add("request.setValue(\"application/json\", forHTTPHeaderField: \"Content-Type\")");
                lines.Add("request.httpBody = try encoder.encode(body)");
            }
            else
            {
                // Best-effort fallback: treat as application/octet-stream.
                lines.Add("request.setValue(\"application/octet-stream\", forHTTPHeaderField: \"Content-Type\")");
                lines.Add("request.httpBody = try encoder.encode(body)");
            }
        }

        lines.Add("let (data, response) = try await session.data(for: request)");
        lines.Add("guard let httpResponse = response as? HTTPURLResponse, (200..<300).contains(httpResponse.statusCode) else {");
        lines.Add("    throw URLError(.badServerResponse)");
        lines.Add("}");

        if (isVoidReturn)
        {
            // Nothing to return; data + response intentionally unused.
            lines.Add("_ = data");
        }
        else if (isBinaryReturn)
        {
            lines.Add("return data");
        }
        else if (returnType != null)
        {
            // Use the rendered Swift type name to drive JSONDecoder.decode.
            var typeText = RenderType(returnType);
            lines.Add($"return try decoder.decode({typeText}.self, from: data)");
        }

        return lines;
    }

    private string RenderType(TypeNode type) => type switch
    {
        SimpleTypeNode s => s.Name.Name + (s.IsNullable ? "?" : string.Empty),
        ArrayTypeNode a  => $"[{RenderType(a.ElementType)}]",
        DictionaryTypeNode d => $"[{RenderType(d.KeyType)}: {RenderType(d.ValueType)}]",
        NullableTypeNode n => RenderType(n.InnerType) + "?",
        _ => "AnyCodable",
    };

    private string ToClassName(string clientName)
    {
        var pascal = _namingConverter.ToPascalCase(clientName);
        return pascal.EndsWith("Api", StringComparison.Ordinal) ? pascal : pascal + "Api";
    }

    private static string EscapeStringLiteral(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal);
}
