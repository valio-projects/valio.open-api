using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Angular.Ast.Nodes;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.Angular.Ast.Builders;

/// <summary>
/// Построитель AST для Angular RxJS HttpClient сервиса.
/// Генерирует один SourceFile на IrClient: класс с <c>@Injectable({ providedIn: 'root' })</c>,
/// конструкторной инъекцией HttpClient и одним методом на операцию, возвращающим
/// <c>Observable&lt;{Op}Result&gt;</c>.
/// </summary>
public sealed class AngularServiceAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly Func<IrType, TypeNode> _typeBuilder;
    private readonly TypeScriptParamsAstBuilder _paramsBuilder;
    private readonly TypeScriptResultsAstBuilder _resultsBuilder;
    private readonly TypeScriptGenerationOptions? _options;

    public AngularServiceAstBuilder(
        INamingConverter namingConverter,
        Func<IrType, TypeNode> typeBuilder,
        TypeScriptGenerationOptions? options = null)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeBuilder = typeBuilder ?? throw new ArgumentNullException(nameof(typeBuilder));
        _options = options;
        _paramsBuilder = new TypeScriptParamsAstBuilder(_namingConverter, _typeBuilder);
        _resultsBuilder = new TypeScriptResultsAstBuilder(_namingConverter, _typeBuilder);
    }

    /// <summary>
    /// Имя файла сервиса (camelCase) — например, <c>userService.service.ts</c>.
    /// </summary>
    public string ServiceFileName(IrClient client)
    {
        var pascal = _namingConverter.ToPascalCase(client.Name);
        var withSuffix = pascal.EndsWith("Service") ? pascal : pascal + "Service";
        return _namingConverter.ToCamelCase(withSuffix);
    }

    /// <summary>
    /// Имя класса сервиса (PascalCase) — например, <c>UserService</c>.
    /// </summary>
    public string ServiceClassName(IrClient client)
    {
        var pascal = _namingConverter.ToPascalCase(client.Name);
        return pascal.EndsWith("Service") ? pascal : pascal + "Service";
    }

    /// <summary>
    /// Строит SourceFile с Angular @Injectable сервисом для одного IrClient.
    /// </summary>
    public SourceFileNode BuildServiceSourceFile(
        IrClient client,
        FileOrganization? fileOrganization = null)
    {
        var imports = BuildImports(client, fileOrganization);

        var className = ServiceClassName(client);
        var attributes = ImmutableList.Create(BuildInjectableDecorator());

        var ctorParameters = ImmutableList.Create(BuildHttpClientCtorParam());

        var members = client.Operations
            .Select(op => (DeclarationNode)BuildOperationMethod(op))
            .ToImmutableList();

        var classNode = new TypeScriptClassDeclarationNode(
            name: new IdentifierNode(className),
            members: members,
            constructorParameters: ctorParameters,
            attributes: attributes,
            documentation: new CommentNode($"Angular HttpClient service for {client.Name}.", CommentKind.JSDoc),
            isExported: true);

        var declarations = ImmutableList.Create<DeclarationNode>(classNode);
        var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();

        return new SourceFileNode(
            imports: imports,
            exports: exports,
            declarations: declarations);
    }

    private AttributeNode BuildInjectableDecorator()
    {
        var providedIn = new PropertyAssignmentNode(
            new IdentifierNode("providedIn"),
            new LiteralExpressionNode("root", LiteralKind.String));

        var configObject = new ObjectLiteralExpressionNode(
            new[] { providedIn }.ToImmutableList(),
            preferMultilineFormat: false);

        return new AttributeNode(
            new IdentifierNode("Injectable"),
            new ExpressionNode[] { configObject }.ToImmutableList());
    }

    private ParameterDeclarationNode BuildHttpClientCtorParam()
    {
        // Конструктор Angular DI: `private readonly http: HttpClient`. Маркируется через metadata.
        var metadata = new Dictionary<string, object>
        {
            ["accessModifier"] = "private",
            ["isReadonly"] = true
        };

        return new ParameterDeclarationNode(
            new IdentifierNode("http"),
            new SimpleTypeNode(new IdentifierNode("HttpClient"), isPrimitive: false),
            isRequired: true,
            metadata: metadata);
    }

    private MethodDeclarationNode BuildOperationMethod(IrOperation operation)
    {
        var methodName = _namingConverter.ToCamelCase(operation.MethodName);
        var parameters = BuildMethodParameters(operation);
        var returnType = BuildReturnType(operation);
        var body = BuildMethodBody(operation);

        var summary = operation.Summary ?? operation.Description ?? $"{operation.HttpMethod} {operation.Path}";

        return new MethodDeclarationNode(
            name: new IdentifierNode(methodName),
            returnType: returnType,
            parameters: parameters,
            body: body,
            isAsync: false,
            accessModifier: AccessModifier.Public,
            documentation: new CommentNode(summary, CommentKind.JSDoc));
    }

    private IReadOnlyList<ParameterDeclarationNode> BuildMethodParameters(IrOperation operation)
    {
        var hasParams = _paramsBuilder.HasParameters(operation);
        if (!hasParams)
            return ImmutableList<ParameterDeclarationNode>.Empty;

        var paramsTypeName = _paramsBuilder.ParamsInterfaceName(operation.MethodName);
        return ImmutableList.Create(new ParameterDeclarationNode(
            new IdentifierNode("params"),
            new SimpleTypeNode(new IdentifierNode(paramsTypeName), isPrimitive: false),
            isRequired: true));
    }

    private TypeNode BuildReturnType(IrOperation operation)
    {
        TypeNode bodyType;
        if (_resultsBuilder.HasResponsesWithBody(operation))
        {
            var resultName = _resultsBuilder.ResultTypeName(operation.MethodName);
            bodyType = new SimpleTypeNode(new IdentifierNode(resultName), isPrimitive: false);
        }
        else
        {
            bodyType = new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true);
        }

        return new GenericTypeNode(
            new IdentifierNode("Observable"),
            new[] { bodyType }.ToImmutableList());
    }

    private BlockNode BuildMethodBody(IrOperation operation)
    {
        var statements = new List<StatementNode>();
        var hasParams = _paramsBuilder.HasParameters(operation);
        var hasRoute = operation.PathParameters.Count > 0;
        var hasQuery = operation.QueryParameters.Count > 0;
        var hasHeaders = operation.HeaderParameters.Count > 0;
        var rb = operation.RequestBody;
        var hasBody = rb != null && (rb.IsJson || rb.IsForm);

        var groupCount = (hasRoute ? 1 : 0) + (hasQuery ? 1 : 0) + (hasBody ? 1 : 0) + (hasHeaders ? 1 : 0);
        var isFlat = groupCount <= 1;

        var paramsExpr = new IdentifierExpressionNode(new IdentifierNode("params"));

        // URL: либо строка-литерал, либо template literal с подстановкой route-параметров.
        var urlSource = hasRoute && hasParams
            ? (isFlat ? (ExpressionNode)paramsExpr : new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("route")))
            : null;
        var urlExpr = BuildUrlExpression(operation, urlSource);

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("url"),
            type: null,
            initializer: urlExpr));

        // Options object (params + headers) — формируется отдельной переменной для читаемости.
        var optionsProps = new List<PropertyAssignmentNode>();

        if (hasQuery && hasParams)
        {
            var querySource = isFlat ? (ExpressionNode)paramsExpr : new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("query"));
            var httpParamsExpr = BuildHttpParamsExpression(operation, querySource);
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("httpParams"),
                type: null,
                initializer: httpParamsExpr));
            optionsProps.Add(new PropertyAssignmentNode(
                new IdentifierNode("params"),
                new IdentifierExpressionNode(new IdentifierNode("httpParams"))));
        }

        if (hasHeaders && hasParams)
        {
            var headersSource = isFlat ? (ExpressionNode)paramsExpr : new MemberAccessExpressionNode(paramsExpr, new IdentifierNode("headers"));
            var httpHeadersExpr = BuildHttpHeadersExpression(operation, headersSource);
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("httpHeaders"),
                type: null,
                initializer: httpHeadersExpr));
            optionsProps.Add(new PropertyAssignmentNode(
                new IdentifierNode("headers"),
                new IdentifierExpressionNode(new IdentifierNode("httpHeaders"))));
        }

        // body: для POST/PUT/PATCH/DELETE с RequestBody передаётся отдельным аргументом.
        ExpressionNode? bodyArg = null;
        if (hasBody && hasParams)
        {
            var bodySource = isFlat ? (ExpressionNode)paramsExpr : new MemberAccessExpressionNode(paramsExpr, new IdentifierNode(rb!.IsJson ? "body" : "form"));
            bodyArg = bodySource;
        }

        // Сборка вызова this.http.{verb}<TResult>(url, [body,] options?).
        var verb = MapHttpMethodToAngular(operation.HttpMethod);
        var resultType = _resultsBuilder.HasResponsesWithBody(operation)
            ? new SimpleTypeNode(new IdentifierNode(_resultsBuilder.ResultTypeName(operation.MethodName)), isPrimitive: false)
            : (TypeNode)new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true);

        // this.http.{verb}<T>
        var thisHttp = new MemberAccessExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("this")),
            new IdentifierNode("http"));
        var verbAccess = new MemberAccessExpressionNode(thisHttp, new IdentifierNode($"{verb}<{TypeNodeToString(resultType)}>"));

        var callArguments = new List<ExpressionNode>
        {
            new IdentifierExpressionNode(new IdentifierNode("url"))
        };

        // Для verb с body (POST/PUT/PATCH/DELETE) body идёт вторым аргументом, options — третьим.
        var verbCarriesBody = verb is "post" or "put" or "patch" or "delete";
        if (verbCarriesBody)
        {
            callArguments.Add(bodyArg ?? new LiteralExpressionNode((object)"", LiteralKind.Null));
        }

        if (optionsProps.Count > 0)
        {
            callArguments.Add(new ObjectLiteralExpressionNode(
                optionsProps.ToImmutableList(),
                preferMultilineFormat: false));
        }

        var call = new CallExpressionNode(verbAccess, callArguments.ToImmutableList());

        statements.Add(new ReturnStatementNode(call));

        return new BlockNode(statements.ToImmutableList());
    }

    /// <summary>
    /// Простое строковое представление TypeNode для генерических имён методов. Используется
    /// только в имени метода (<c>get&lt;T&gt;</c>) — финальный рендер делает renderer.
    /// </summary>
    private static string TypeNodeToString(TypeNode type)
    {
        return type switch
        {
            SimpleTypeNode st => st.Name.Name,
            GenericTypeNode gt => gt.Name.Name + "<" + string.Join(", ", gt.TypeArguments.Select(TypeNodeToString)) + ">",
            _ => type.ToString() ?? "unknown"
        };
    }

    private static string MapHttpMethodToAngular(string httpMethod)
    {
        return httpMethod.ToUpperInvariant() switch
        {
            "GET" => "get",
            "POST" => "post",
            "PUT" => "put",
            "PATCH" => "patch",
            "DELETE" => "delete",
            "HEAD" => "head",
            "OPTIONS" => "options",
            _ => "request"
        };
    }

    private ExpressionNode BuildUrlExpression(IrOperation operation, ExpressionNode? routeSource)
    {
        var path = operation.Path;
        if (operation.PathParameters.Count == 0 || routeSource == null)
            return new LiteralExpressionNode(path, LiteralKind.String);

        var parts = new List<TemplateLiteralPart>();
        var currentPath = path;

        foreach (var param in operation.PathParameters)
        {
            var placeholder = "{" + param.Name + "}";
            var idx = currentPath.IndexOf(placeholder, StringComparison.Ordinal);
            if (idx < 0)
                continue;

            if (idx > 0)
                parts.Add(new TemplateLiteralPart(currentPath.Substring(0, idx)));

            parts.Add(new TemplateLiteralPart(new MemberAccessExpressionNode(routeSource, new IdentifierNode(param.Name))));

            currentPath = currentPath.Substring(idx + placeholder.Length);
        }

        if (currentPath.Length > 0)
            parts.Add(new TemplateLiteralPart(currentPath));

        return parts.Count > 0
            ? new TemplateLiteralExpressionNode(parts.ToImmutableList())
            : new LiteralExpressionNode(path, LiteralKind.String);
    }

    /// <summary>
    /// Строит <c>new HttpParams().set("a", String(query.a)).set(...)</c> с цепочкой <c>.set()</c> на каждый query-параметр.
    /// Для optional значений добавляется проверка <c>!= null</c>.
    /// </summary>
    private ExpressionNode BuildHttpParamsExpression(IrOperation operation, ExpressionNode querySource)
    {
        var newCall = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("new HttpParams")),
            ImmutableList<ExpressionNode>.Empty);

        ExpressionNode chain = newCall;
        foreach (var qp in operation.QueryParameters)
        {
            var valueAccess = new MemberAccessExpressionNode(querySource, new IdentifierNode(qp.Name));
            var stringify = new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("String")),
                new ExpressionNode[] { valueAccess }.ToImmutableList());

            chain = new CallExpressionNode(
                new MemberAccessExpressionNode(chain, new IdentifierNode("set")),
                new ExpressionNode[]
                {
                    new LiteralExpressionNode(qp.Name, LiteralKind.String),
                    stringify
                }.ToImmutableList());
        }
        return chain;
    }

    /// <summary>
    /// Строит <c>new HttpHeaders().set("X-A", String(headers.a))</c>.
    /// </summary>
    private ExpressionNode BuildHttpHeadersExpression(IrOperation operation, ExpressionNode headersSource)
    {
        var newCall = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("new HttpHeaders")),
            ImmutableList<ExpressionNode>.Empty);

        ExpressionNode chain = newCall;
        foreach (var hp in operation.HeaderParameters)
        {
            var valueAccess = new MemberAccessExpressionNode(headersSource, new IdentifierNode(hp.Name));
            var stringify = new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("String")),
                new ExpressionNode[] { valueAccess }.ToImmutableList());

            chain = new CallExpressionNode(
                new MemberAccessExpressionNode(chain, new IdentifierNode("set")),
                new ExpressionNode[]
                {
                    new LiteralExpressionNode(hp.Name, LiteralKind.String),
                    stringify
                }.ToImmutableList());
        }
        return chain;
    }

    /// <summary>
    /// Импорты: Angular + RxJS + params/results интерфейсы + модели тел запросов/ответов.
    /// </summary>
    private IReadOnlyList<ImportNode> BuildImports(IrClient client, FileOrganization? fileOrganization)
    {
        var imports = new List<ImportNode>();

        imports.Add(new ImportNode(
            items: new[] { new IdentifierNode("Injectable") }.ToImmutableList(),
            from: "@angular/core",
            kind: ImportKind.Named));

        imports.Add(new ImportNode(
            items: new[] { new IdentifierNode("HttpClient"), new IdentifierNode("HttpHeaders"), new IdentifierNode("HttpParams") }.ToImmutableList(),
            from: "@angular/common/http",
            kind: ImportKind.Named));

        imports.Add(new ImportNode(
            items: new[] { new IdentifierNode("Observable") }.ToImmutableList(),
            from: "rxjs",
            kind: ImportKind.Named));

        var clientsPath = fileOrganization?.ClientsPath ?? "api";
        var modelsPath = fileOrganization?.ModelsPath ?? "models";

        var modelDeps = new HashSet<string>();

        foreach (var op in client.Operations)
        {
            var opFile = _namingConverter.ToCamelCase(op.MethodName);

            if (_paramsBuilder.HasParameters(op))
            {
                var paramsTypeName = _paramsBuilder.ParamsInterfaceName(op.MethodName);
                imports.Add(new ImportNode(
                    items: new[] { new IdentifierNode(paramsTypeName) }.ToImmutableList(),
                    from: $"./{opFile}/{opFile}.params",
                    kind: ImportKind.Named,
                    isTypeOnly: true));
            }

            if (_resultsBuilder.HasResponsesWithBody(op))
            {
                var resultName = _resultsBuilder.ResultTypeName(op.MethodName);
                imports.Add(new ImportNode(
                    items: new[] { new IdentifierNode(resultName) }.ToImmutableList(),
                    from: $"./{opFile}/{opFile}.results",
                    kind: ImportKind.Named,
                    isTypeOnly: true));
            }

            // Модели/enums из request body — нужны как типы для body аргумента (для типизации).
            if (op.RequestBody?.Type is IrModelReference reqRef)
                modelDeps.Add(reqRef.ModelName);
            foreach (var resp in op.Responses)
            {
                if (resp.Type is IrModelReference rRef)
                    modelDeps.Add(rRef.ModelName);
            }
        }

        // Импорты моделей пропускаем — типы приходят через {Op}Params/{Op}Result, всё остальное косвенно.
        // Если в будущем сервис будет принимать body отдельным параметром — здесь добавятся импорты модели.
        _ = modelDeps;
        _ = clientsPath;
        _ = modelsPath;

        return imports;
    }
}
