using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Python.Ast.Nodes;
using ValIO.Generators.Python.Options;

namespace ValIO.Generators.Python.Ast.Builders;

/// <summary>
/// Строит HTTPX-клиент (один класс на IrClient) в виде Python-модуля.
/// </summary>
public sealed class HttpxClientAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly PythonIrTypeMapper _typeMapper;

    public HttpxClientAstBuilder(INamingConverter namingConverter, PythonIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

    public PythonModuleNode BuildModule(
        IrClient client,
        IReadOnlyDictionary<string, string> modelModuleByName,
        IReadOnlyDictionary<string, string> enumModuleByName,
        PythonGenerationOptions options)
    {
        var isAsync = options.HttpxMode == PythonOptionIds.HttpxMode.Async;
        var className = ToClassName(client.Name);

        // Сбор импортов.
        var modelImports = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var datetimeImports = new SortedSet<string>(StringComparer.Ordinal);
        var uuidNeeded = false;
        var anyNeeded = false;
        var optionalNeeded = false;

        // Собираем методы клиента.
        var methods = new List<DeclarationNode>();
        foreach (var op in client.Operations)
        {
            var method = BuildOperationMethod(
                op,
                isAsync,
                modelImports,
                modelModuleByName,
                enumModuleByName,
                datetimeImports,
                ref uuidNeeded,
                ref anyNeeded,
                ref optionalNeeded);
            methods.Add(method);
        }

        // Конструктор.
        var ctor = BuildConstructor(isAsync);
        methods.Insert(0, ctor);

        // Модельные импорты используются runtime (Pet.model_validate) — обычные импорты, не TYPE_CHECKING.
        var imports = new List<PythonImportNode>();

        var typingNames = new List<string>();
        if (anyNeeded) typingNames.Add("Any");
        if (typingNames.Count > 0)
            imports.Add(new PythonImportNode("typing", typingNames));

        if (datetimeImports.Count > 0)
            imports.Add(new PythonImportNode("datetime", datetimeImports.ToList()));

        if (uuidNeeded)
            imports.Add(new PythonImportNode("uuid", new[] { "UUID" }));

        imports.Add(new PythonImportNode("httpx", names: Array.Empty<string>()));

        foreach (var kvp in modelImports)
            imports.Add(new PythonImportNode(kvp.Value, new[] { kvp.Key }));

        var typeCheckingImports = new List<PythonImportNode>();

        var classNode = new PythonClassDeclarationNode(
            new IdentifierNode(className),
            baseClasses: null,
            members: methods,
            documentation: BuildClientDocstring(client));

        return new PythonModuleNode(
            declarations: new DeclarationNode[] { classNode },
            imports: imports,
            typeCheckingImports: typeCheckingImports,
            futureImports: new[] { new PythonImportNode("__future__", new[] { "annotations" }) });
    }

    private static CommentNode BuildClientDocstring(IrClient client) =>
        new CommentNode($"HTTP client for {client.Name}.", CommentKind.MultiLine);

    private static PythonFunctionDeclarationNode BuildConstructor(bool isAsync)
    {
        var clientAnnotation = new SimpleTypeNode(
            new IdentifierNode(isAsync ? "httpx.AsyncClient" : "httpx.Client"),
            isPrimitive: true);

        var parameters = new[]
        {
            new PythonParameterNode(new IdentifierNode("self")),
            new PythonParameterNode(new IdentifierNode("client"), clientAnnotation),
        };

        return new PythonFunctionDeclarationNode(
            new IdentifierNode("__init__"),
            parameters,
            returnType: new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true),
            body: new[] { "self._client = client" });
    }

    private PythonFunctionDeclarationNode BuildOperationMethod(
        IrOperation op,
        bool isAsync,
        SortedDictionary<string, string> modelImports,
        IReadOnlyDictionary<string, string> modelModuleByName,
        IReadOnlyDictionary<string, string> enumModuleByName,
        SortedSet<string> datetimeImports,
        ref bool uuidNeeded,
        ref bool anyNeeded,
        ref bool optionalNeeded)
    {
        var methodName = _namingConverter.ToSnakeCase(op.MethodName);
        methodName = PythonKeywords.Avoid(methodName);

        // Параметры: self + path + query + body.
        var parameters = new List<PythonParameterNode>
        {
            new PythonParameterNode(new IdentifierNode("self"))
        };

        // Path-параметры (required).
        foreach (var p in op.PathParameters)
        {
            var annotation = _typeMapper.MapType(p.Type);
            ScanForImports(p.Type, modelImports, modelModuleByName, enumModuleByName,
                datetimeImports, ref uuidNeeded, ref anyNeeded);
            parameters.Add(new PythonParameterNode(
                new IdentifierNode(PythonKeywords.Avoid(_namingConverter.ToSnakeCase(p.Name))),
                annotation));
        }

        // Request body.
        if (op.RequestBody != null)
        {
            var annotation = _typeMapper.MapType(op.RequestBody.Type);
            ScanForImports(op.RequestBody.Type, modelImports, modelModuleByName, enumModuleByName,
                datetimeImports, ref uuidNeeded, ref anyNeeded);
            parameters.Add(new PythonParameterNode(
                new IdentifierNode("body"),
                annotation));
        }

        // Query-параметры (optional → default None).
        foreach (var p in op.QueryParameters)
        {
            var annotation = _typeMapper.MapType(p.Type);
            ScanForImports(p.Type, modelImports, modelModuleByName, enumModuleByName,
                datetimeImports, ref uuidNeeded, ref anyNeeded);
            string? defaultValue = null;
            if (!p.IsRequired)
            {
                annotation = AddNone(annotation);
                defaultValue = "None";
            }
            parameters.Add(new PythonParameterNode(
                new IdentifierNode(PythonKeywords.Avoid(_namingConverter.ToSnakeCase(p.Name))),
                annotation,
                defaultValue));
        }

        // Determine response type.
        var successResponse = op.Responses.FirstOrDefault(r => r.StatusCode.StartsWith("2", StringComparison.Ordinal));
        TypeNode? returnType = null;
        string? responseModelName = null;
        if (successResponse?.Type != null)
        {
            returnType = _typeMapper.MapType(successResponse.Type);
            ScanForImports(successResponse.Type, modelImports, modelModuleByName, enumModuleByName,
                datetimeImports, ref uuidNeeded, ref anyNeeded);
            responseModelName = TryGetSimpleModelName(successResponse.Type);
        }
        else
        {
            returnType = new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true);
        }

        var body = BuildMethodBody(op, isAsync, responseModelName, successResponse?.Type);

        return new PythonFunctionDeclarationNode(
            new IdentifierNode(methodName),
            parameters,
            returnType: WrapForwardRef(returnType, responseModelName != null && modelImports.ContainsKey(responseModelName)),
            body: body,
            isAsync: isAsync,
            documentation: BuildOperationDocstring(op));
    }

    private static TypeNode WrapForwardRef(TypeNode type, bool quote)
    {
        // Возвращаемый тип в годовых аннотациях всегда оборачивается в "" если ссылается на модель.
        // Текущий рендерер для return type не делает quoting автоматически; полагаемся на
        // `from __future__ import annotations` чтобы все аннотации стали строками.
        return type;
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
        bool isAsync,
        string? responseModelName,
        IrType? responseType)
    {
        var lines = new List<string>();

        // Формируем URL: f"/path/{param}".
        var pathExpr = ToFStringPath(op.Path, op.PathParameters);
        var awaitKeyword = isAsync ? "await " : "";
        var httpMethod = op.HttpMethod.ToLowerInvariant();
        var requestArgs = new List<string> { pathExpr };

        // Query params.
        if (op.QueryParameters.Count > 0)
        {
            lines.Add("params: dict[str, Any] = {}");
            foreach (var p in op.QueryParameters)
            {
                var pyName = PythonKeywords.Avoid(_namingConverter.ToSnakeCase(p.Name));
                if (p.IsRequired)
                    lines.Add($"params[\"{p.Name}\"] = {pyName}");
                else
                    lines.Add($"if {pyName} is not None: params[\"{p.Name}\"] = {pyName}");
            }
            requestArgs.Add("params=params");
        }

        // Request body.
        if (op.RequestBody != null)
        {
            if (op.RequestBody.IsJson || (op.RequestBody.ContentType == null))
            {
                // Для Pydantic-модели вызываем model_dump(by_alias=True).
                if (IsPydanticModelRef(op.RequestBody.Type))
                    requestArgs.Add("json=body.model_dump(by_alias=True, exclude_none=True)");
                else
                    requestArgs.Add("json=body");
            }
            else if (op.RequestBody.IsFormUrlEncoded)
            {
                requestArgs.Add("data=body");
            }
            else if (op.RequestBody.IsMultipartFormData)
            {
                requestArgs.Add("files=body");
            }
            else
            {
                requestArgs.Add("content=body");
            }
        }

        var argList = string.Join(", ", requestArgs);
        lines.Add($"response = {awaitKeyword}self._client.{httpMethod}({argList})");
        lines.Add("response.raise_for_status()");

        if (responseType != null)
        {
            if (responseModelName != null)
            {
                lines.Add($"return {responseModelName}.model_validate(response.json())");
            }
            else if (responseType is IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Void })
            {
                lines.Add("return None");
            }
            else if (responseType is IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File })
            {
                lines.Add("return response.content");
            }
            else
            {
                lines.Add("return response.json()");
            }
        }
        else
        {
            lines.Add("return None");
        }
        return lines;
    }

    private static bool IsPydanticModelRef(IrType type) => type switch
    {
        IrModelReference => true,
        IrNullableType n => IsPydanticModelRef(n.InnerType),
        IrOptionalType o => IsPydanticModelRef(o.InnerType),
        _ => false
    };

    private static string? TryGetSimpleModelName(IrType type) => type switch
    {
        IrModelReference r => r.ModelName,
        IrNullableType n => TryGetSimpleModelName(n.InnerType),
        IrOptionalType o => TryGetSimpleModelName(o.InnerType),
        _ => null
    };

    private static TypeNode AddNone(TypeNode inner)
    {
        if (inner is UnionTypeNode u && u.Types.Any(t => t is SimpleTypeNode s && s.Name.Name == "None"))
            return inner;
        if (inner is UnionTypeNode existing)
            return new UnionTypeNode(existing.Types.Concat(new[] { (TypeNode)new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true) }).ToList());
        return new UnionTypeNode(new[] { inner, (TypeNode)new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true) });
    }

    private string ToFStringPath(string path, IReadOnlyList<IrParameter> pathParams)
    {
        // OpenAPI: "/users/{id}". Преобразуем имена к snake_case в выражении f-string.
        var result = path;
        foreach (var p in pathParams)
        {
            var snake = PythonKeywords.Avoid(_namingConverter.ToSnakeCase(p.Name));
            result = result.Replace("{" + p.Name + "}", "{" + snake + "}", StringComparison.Ordinal);
        }
        return $"f\"{result}\"";
    }

    private static void ScanForImports(
        IrType type,
        SortedDictionary<string, string> modelImports,
        IReadOnlyDictionary<string, string> modelModuleByName,
        IReadOnlyDictionary<string, string> enumModuleByName,
        SortedSet<string> datetimeImports,
        ref bool uuidNeeded,
        ref bool anyNeeded)
    {
        switch (type)
        {
            case IrPrimitiveType p:
                switch (p.PrimitiveKind)
                {
                    case IrPrimitiveKind.Date: datetimeImports.Add("date"); break;
                    case IrPrimitiveKind.DateTime: datetimeImports.Add("datetime"); break;
                    case IrPrimitiveKind.Time: datetimeImports.Add("time"); break;
                    case IrPrimitiveKind.Uuid: uuidNeeded = true; break;
                    case IrPrimitiveKind.Any: anyNeeded = true; break;
                }
                break;
            case IrModelReference r:
                if (modelModuleByName.TryGetValue(r.ModelName, out var module))
                    modelImports[r.ModelName] = module;
                else if (enumModuleByName.TryGetValue(r.ModelName, out var enumModule))
                    modelImports[r.ModelName] = enumModule;
                break;
            case IrArrayType a:
                ScanForImports(a.ElementType, modelImports, modelModuleByName, enumModuleByName, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrDictionaryType d:
                ScanForImports(d.KeyType, modelImports, modelModuleByName, enumModuleByName, datetimeImports, ref uuidNeeded, ref anyNeeded);
                ScanForImports(d.ValueType, modelImports, modelModuleByName, enumModuleByName, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrNullableType n:
                ScanForImports(n.InnerType, modelImports, modelModuleByName, enumModuleByName, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrOptionalType o:
                ScanForImports(o.InnerType, modelImports, modelModuleByName, enumModuleByName, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrUnionType u:
                foreach (var t in u.Types) ScanForImports(t, modelImports, modelModuleByName, enumModuleByName, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrIntersectionType i:
                foreach (var t in i.Types) ScanForImports(t, modelImports, modelModuleByName, enumModuleByName, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
        }
    }

    private string ToClassName(string clientName)
    {
        var pascal = _namingConverter.ToPascalCase(clientName);
        return pascal.EndsWith("Api", StringComparison.Ordinal) ? pascal : pascal + "Api";
    }
}
