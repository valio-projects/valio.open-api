using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Kotlin.Ast.Nodes;

namespace ValIO.Generators.Kotlin.Ast.Builders;

/// <summary>
/// Builds an OkHttp + coroutines service class for one <see cref="IrClient"/>.
/// </summary>
public sealed class OkHttpClientAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly KotlinIrTypeMapper _typeMapper;

    public OkHttpClientAstBuilder(INamingConverter namingConverter, KotlinIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

    public KotlinFileNode BuildFile(
        IrClient client,
        string apiPackage,
        string modelsPackage,
        IReadOnlySet<string> knownModelNames)
    {
        var className = ToClassName(client.Name);
        var importScan = new KotlinTypeImportScanner.Result();

        // Primary-constructor properties.
        var ctorProps = new List<KotlinPropertyNode>
        {
            new KotlinPropertyNode(
                new IdentifierNode("client"),
                new SimpleTypeNode(new IdentifierNode("OkHttpClient"), isPrimitive: true),
                visibility: "private",
                isReadOnly: true,
                isConstructorParameter: true),
            new KotlinPropertyNode(
                new IdentifierNode("baseUrl"),
                new SimpleTypeNode(new IdentifierNode("HttpUrl"), isPrimitive: true),
                visibility: "private",
                isReadOnly: true,
                isConstructorParameter: true),
            new KotlinPropertyNode(
                new IdentifierNode("json"),
                new SimpleTypeNode(new IdentifierNode("Json"), isPrimitive: true),
                visibility: "private",
                isReadOnly: true,
                isConstructorParameter: true),
        };

        var methods = new List<DeclarationNode>();
        foreach (var op in client.Operations)
        {
            methods.Add(BuildOperationMethod(op, importScan));
        }

        var classNode = new KotlinClassDeclarationNode(
            new IdentifierNode(className),
            ctorProps,
            methods,
            annotations: null,
            visibility: null,
            documentation: new CommentNode($"HTTP client for {client.Name}.", CommentKind.MultiLine));

        // Standard imports: always OkHttp + coroutines + kotlinx.serialization.json.
        var imports = new List<KotlinImportNode>
        {
            new KotlinImportNode("kotlinx.coroutines.Dispatchers"),
            new KotlinImportNode("kotlinx.coroutines.withContext"),
            new KotlinImportNode("kotlinx.serialization.json.Json"),
            new KotlinImportNode("okhttp3.HttpUrl"),
            new KotlinImportNode("okhttp3.MediaType.Companion.toMediaType"),
            new KotlinImportNode("okhttp3.OkHttpClient"),
            new KotlinImportNode("okhttp3.Request"),
            new KotlinImportNode("okhttp3.RequestBody.Companion.toRequestBody"),
        };
        foreach (var stdImport in importScan.StandardImports)
            imports.Add(new KotlinImportNode(stdImport));
        foreach (var modelName in importScan.ModelReferences)
        {
            if (!knownModelNames.Contains(modelName)) continue;
            imports.Add(new KotlinImportNode($"{modelsPackage}.{modelName}"));
        }

        return new KotlinFileNode(apiPackage, declarations: [classNode], imports: imports);
    }

    private KotlinFunctionDeclarationNode BuildOperationMethod(
        IrOperation op,
        KotlinTypeImportScanner.Result importScan)
    {
        var methodName = KotlinReservedNames.Avoid(_namingConverter.ToCamelCase(op.MethodName));
        var parameters = new List<KotlinParameterNode>();

        foreach (var p in op.PathParameters)
        {
            KotlinTypeImportScanner.Walk(p.Type, importScan);
            parameters.Add(new KotlinParameterNode(
                new IdentifierNode(KotlinReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name))),
                _typeMapper.MapType(p.Type)));
        }

        if (op.RequestBody != null)
        {
            KotlinTypeImportScanner.Walk(op.RequestBody.Type, importScan);
            parameters.Add(new KotlinParameterNode(
                new IdentifierNode("body"),
                _typeMapper.MapType(op.RequestBody.Type)));
        }

        foreach (var p in op.QueryParameters)
        {
            KotlinTypeImportScanner.Walk(p.Type, importScan);
            var annotation = _typeMapper.MapType(p.Type);
            string? defaultValue = null;
            if (!p.IsRequired)
            {
                annotation = annotation is NullableTypeNode ? annotation : new NullableTypeNode(annotation);
                defaultValue = "null";
            }
            parameters.Add(new KotlinParameterNode(
                new IdentifierNode(KotlinReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name))),
                annotation,
                defaultValue));
        }

        // Pick the first 2xx response as success.
        var successResponse = op.Responses.FirstOrDefault(r => r.StatusCode.StartsWith("2", StringComparison.Ordinal));
        TypeNode returnType;
        string? responseModelName = null;
        bool isVoidReturn = false;
        bool isBinaryReturn = false;
        if (successResponse?.Type != null)
        {
            KotlinTypeImportScanner.Walk(successResponse.Type, importScan);
            returnType = _typeMapper.MapType(successResponse.Type);
            responseModelName = TryGetSimpleModelName(successResponse.Type);
            isBinaryReturn = successResponse.Type is IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File };
        }
        else
        {
            returnType = new SimpleTypeNode(new IdentifierNode("Unit"), isPrimitive: true);
            isVoidReturn = true;
        }

        var body = BuildMethodBody(op, responseModelName, isVoidReturn, isBinaryReturn);

        return new KotlinFunctionDeclarationNode(
            new IdentifierNode(methodName),
            parameters,
            returnType,
            body,
            isSuspend: true,
            annotations: null,
            visibility: null,
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

    private IReadOnlyList<string> BuildMethodBody(
        IrOperation op,
        string? responseModelName,
        bool isVoidReturn,
        bool isBinaryReturn)
    {
        var lines = new List<string>
        {
            "withContext(Dispatchers.IO) {",
            "    val urlBuilder = baseUrl.newBuilder()",
        };

        // Path template — replace {name} with $name (Kotlin string templates).
        var pathTemplate = op.Path;
        foreach (var p in op.PathParameters)
        {
            var camel = KotlinReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name));
            pathTemplate = pathTemplate.Replace("{" + p.Name + "}", "${" + camel + "}", StringComparison.Ordinal);
        }
        var trimmedPath = pathTemplate.TrimStart('/');
        lines.Add($"    urlBuilder.addPathSegments(\"{EscapeStringLiteral(trimmedPath)}\")");

        // Query parameters.
        foreach (var p in op.QueryParameters)
        {
            var camel = KotlinReservedNames.Avoid(_namingConverter.ToCamelCase(p.Name));
            if (p.IsRequired)
                lines.Add($"    urlBuilder.addQueryParameter(\"{EscapeStringLiteral(p.Name)}\", {camel}.toString())");
            else
                lines.Add($"    if ({camel} != null) urlBuilder.addQueryParameter(\"{EscapeStringLiteral(p.Name)}\", {camel}.toString())");
        }

        lines.Add("    val url = urlBuilder.build()");
        lines.Add("    val requestBuilder = Request.Builder().url(url)");

        var httpMethod = op.HttpMethod.ToUpperInvariant();
        if (op.RequestBody != null)
        {
            // JSON serialization via kotlinx.serialization.
            if (op.RequestBody.IsJson)
            {
                var bodyModelName = TryGetSimpleModelName(op.RequestBody.Type);
                if (bodyModelName != null)
                {
                    lines.Add($"    val jsonBody = json.encodeToString({bodyModelName}.serializer(), body)");
                    lines.Add("    val requestBody = jsonBody.toRequestBody(\"application/json\".toMediaType())");
                }
                else
                {
                    // Fallback: serialize Any (uses default polymorphic / contextual serializers).
                    lines.Add("    val jsonBody = json.encodeToString(body)");
                    lines.Add("    val requestBody = jsonBody.toRequestBody(\"application/json\".toMediaType())");
                }
            }
            else
            {
                // Treat raw bodies as octet-stream for now (TODO multipart/form support).
                lines.Add("    val requestBody = (body?.toString() ?: \"\").toRequestBody(\"application/octet-stream\".toMediaType())");
            }
            lines.Add($"    requestBuilder.method(\"{httpMethod}\", requestBody)");
        }
        else if (httpMethod is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            // Some verbs require an explicit empty body.
            lines.Add($"    requestBuilder.method(\"{httpMethod}\", \"\".toRequestBody(\"application/json\".toMediaType()))");
        }
        else
        {
            lines.Add($"    requestBuilder.method(\"{httpMethod}\", null)");
        }

        lines.Add("    val request = requestBuilder.build()");
        lines.Add("    client.newCall(request).execute().use { response ->");
        lines.Add("        if (!response.isSuccessful) error(\"HTTP ${response.code}: ${response.message}\")");

        if (isVoidReturn)
        {
            lines.Add("        Unit");
        }
        else if (isBinaryReturn)
        {
            lines.Add("        response.body?.bytes() ?: ByteArray(0)");
        }
        else if (responseModelName != null)
        {
            lines.Add("        val raw = response.body?.string() ?: error(\"Empty body\")");
            lines.Add($"        json.decodeFromString({responseModelName}.serializer(), raw)");
        }
        else
        {
            // Plain primitive — return the body as a String. The caller may need to parse it.
            lines.Add("        response.body?.string() ?: error(\"Empty body\")");
        }

        lines.Add("    }");
        lines.Add("}");
        return lines;
    }

    private static string? TryGetSimpleModelName(IrType type) => type switch
    {
        IrModelReference r => r.ModelName,
        IrNullableType n   => TryGetSimpleModelName(n.InnerType),
        IrOptionalType o   => TryGetSimpleModelName(o.InnerType),
        _ => null,
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
