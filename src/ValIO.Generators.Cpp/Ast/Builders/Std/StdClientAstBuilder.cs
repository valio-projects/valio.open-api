using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.IR.Utilities;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Ast.Builders.Std;

/// <summary>
/// Строитель клиента std-профиля. clientShape:
///  • "class"         — один {Client}Client.h/.cpp;
///  • "interface+impl" — I{Client}.h (чистые виртуальные методы, только модели +
///    Expected/Error — моки/DI) + {Client}Client : public I{Client}.
/// apiStyle: sync / async (std::future) / both. Тела операций — с именованными
/// промежуточными локальными переменными (path / httpRequest / body / sendResult /
/// response) для отладки. wire-имена параметров — IrParameter.OriginalName.
/// </summary>
public sealed class StdClientAstBuilder
{
    private const string ConstructorMetadataKey = "cpp.constructor";

    private readonly StdCppTypeMapper _typeMapper;
    private readonly StdNamingConvention _naming;

    public StdClientAstBuilder(StdCppTypeMapper typeMapper, StdNamingConvention naming)
    {
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
    }

    public sealed class BuildResult
    {
        public CppHeaderFileNode? InterfaceHeader { get; init; }
        public required CppHeaderFileNode ClientHeader { get; init; }
        public required CppSourceFileNode ClientSource { get; init; }
    }

    public BuildResult Build(
        IrClient client,
        IReadOnlySet<string> enumNames,
        IReadOnlyDictionary<string, IrModel> modelLookup,
        string rootNamespace,
        CppGenerationOptions options)
    {
        var clientClassName = _naming.ClientClassName(client.Name);
        var interfaceName = _naming.InterfaceName(client.Name);
        var useInterface = options.ClientShape == CppClientShape.InterfaceImpl;
        var emitSync = options.ApiStyle is CppApiStyle.Sync or CppApiStyle.Both;
        var emitAsync = options.ApiStyle is CppApiStyle.Async or CppApiStyle.Both;

        var referencedModels = new HashSet<string>();
        var referencedEnums = new HashSet<string>();
        foreach (var operation in client.Operations)
            CollectOperationReferences(operation, referencedModels, referencedEnums, enumNames);

        // ===== Интерфейс (только модели + Expected/Error; без runtime-pimpl) =====
        CppHeaderFileNode? interfaceHeader = null;
        if (useInterface)
        {
            var interfaceMethods = new List<DeclarationNode>();
            foreach (var operation in client.Operations)
            {
                if (emitSync)
                    interfaceMethods.Add(BuildMethodDeclaration(operation, isAsync: false, isInterface: true));
                if (emitAsync)
                    interfaceMethods.Add(BuildMethodDeclaration(operation, isAsync: true, isInterface: true));
            }

            interfaceHeader = new CppHeaderFileNode(
                fileName: _naming.InterfaceHeaderPath(client.Name),
                includes: BuildInterfaceIncludes(referencedModels, referencedEnums, enumNames, emitAsync),
                declarations:
                [
                    new NamespaceDeclarationNode(new IdentifierNode(rootNamespace),
                    [
                        new InterfaceDeclarationNode(
                            new IdentifierNode(interfaceName),
                            members: interfaceMethods),
                    ]),
                ]);
        }

        // ===== Класс клиента =====
        var classMembers = new List<DeclarationNode>
        {
            new MethodDeclarationNode(
                new IdentifierNode(clientClassName),
                parameters: [new ParameterDeclarationNode(new IdentifierNode("config"), new RawTypeNode("const ValIO::HttpConfig&"))],
                accessModifier: AccessModifier.Public,
                metadata: new Dictionary<string, object> { [ConstructorMetadataKey] = true }),
        };

        foreach (var operation in client.Operations)
        {
            if (emitSync)
                classMembers.Add(BuildMethodDeclaration(operation, isAsync: false, isInterface: false));
            if (emitAsync)
                classMembers.Add(BuildMethodDeclaration(operation, isAsync: true, isInterface: false));
        }

        classMembers.Add(new FieldDeclarationNode(
            new IdentifierNode("_http"),
            new SimpleTypeNode(new IdentifierNode("ValIO::HttpClient")),
            AccessModifier.Private));

        var clientHeader = new CppHeaderFileNode(
            fileName: _naming.ClientHeaderPath(client.Name),
            includes: BuildClientIncludes(client.Name, referencedModels, referencedEnums, enumNames, useInterface, emitAsync),
            declarations:
            [
                new NamespaceDeclarationNode(new IdentifierNode(rootNamespace),
                [
                    new ClassDeclarationNode(
                        new IdentifierNode(clientClassName),
                        isSealed: true,
                        baseTypes: useInterface ? [new SimpleTypeNode(new IdentifierNode(interfaceName))] : null,
                        members: classMembers),
                ]),
            ]);

        // ===== Реализация (.cpp) =====
        var sourceMembers = new List<AstNode>();
        sourceMembers.Add(BuildConstructorDefinition(clientClassName));
        foreach (var operation in client.Operations)
        {
            if (emitSync)
                sourceMembers.Add(BuildMethodDefinition(clientClassName, operation, enumNames, modelLookup, isAsync: false));
            if (emitAsync)
                sourceMembers.Add(BuildMethodDefinition(clientClassName, operation, enumNames, modelLookup, isAsync: true));
        }

        var clientSource = new CppSourceFileNode(
            fileName: _naming.ClientSourcePath(client.Name),
            includes: BuildSourceIncludes(client.Name, referencedModels, referencedEnums, enumNames, useInterface, options),
            sections:
            [
                new CppSourceSection(members:
                [
                    new NamespaceDeclarationNode(new IdentifierNode(rootNamespace), sourceMembers.Cast<DeclarationNode>().ToList()),
                ]),
            ]);

        return new BuildResult
        {
            InterfaceHeader = interfaceHeader,
            ClientHeader = clientHeader,
            ClientSource = clientSource,
        };
    }

    // ===== Сигнатуры =====

    private MethodDeclarationNode BuildMethodDeclaration(
        IrOperation operation, bool isAsync, bool isInterface)
    {
        var methodName = _naming.MethodName(operation.MethodName);
        return new MethodDeclarationNode(
            new IdentifierNode(isAsync ? $"{methodName}Async" : methodName),
            returnType: BuildExpectedType(GetSuccessType(operation), isAsync),
            parameters: BuildMethodParameters(operation),
            accessModifier: AccessModifier.Public,
            isInterfaceMethod: isInterface,
            isConst: true);
    }

    /// <summary>ValIO::Expected&lt;T, ValIO::Error&gt; (async — обёрнут в std::future).</summary>
    private TypeNode BuildExpectedType(IrType? successType, bool isAsync)
    {
        var valueType = successType == null
            ? new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true)
            : _typeMapper.MapType(successType);
        var expected = new GenericTypeNode(
            new IdentifierNode("ValIO::Expected"),
            [valueType, new SimpleTypeNode(new IdentifierNode("ValIO::Error"))]);
        return isAsync
            ? new GenericTypeNode(new IdentifierNode("std::future"), [expected])
            : expected;
    }

    /// <summary>
    /// Параметры метода: обязательные вперёд (path → query/header required → body),
    /// optional — в хвост с default = std::nullopt (C++ требует trailing-дефолты).
    /// </summary>
    private List<ParameterDeclarationNode> BuildMethodParameters(IrOperation operation)
    {
        var required = new List<ParameterDeclarationNode>();
        var optional = new List<ParameterDeclarationNode>();

        foreach (var parameter in operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters))
        {
            var declaration = BuildParameter(parameter);
            (parameter.IsRequired ? required : optional).Add(declaration);
        }

        if (operation.RequestBody != null)
        {
            required.Add(new ParameterDeclarationNode(
                new IdentifierNode("request"),
                IrTypeQueries.IsFileOrBinary(operation.RequestBody.Type)
                    ? new RawTypeNode("const std::vector<unsigned char>&")
                    : new RawTypeNode($"const {StdTypeText.Render(_typeMapper.MapType(operation.RequestBody.Type))}&")));
        }

        required.AddRange(optional);
        return required;
    }

    private ParameterDeclarationNode BuildParameter(IrParameter parameter)
    {
        var name = _naming.PropertyName(parameter.Name);
        var type = _typeMapper.MapType(parameter.Type);
        var isStructReference = parameter.Type is IrModelReference && !_typeMapper.IsEnumReference(((IrModelReference)parameter.Type).ModelName);

        if (parameter.IsRequired)
        {
            return isStructReference
                ? new ParameterDeclarationNode(new IdentifierNode(name), new RawTypeNode($"const {StdTypeText.Render(type)}&"))
                : new ParameterDeclarationNode(new IdentifierNode(name), type);
        }

        // Optional: std::optional + trailing default.
        TypeNode optionalType = isStructReference
            ? new RawTypeNode($"std::optional<{StdTypeText.Render(type)}>")
            : new GenericTypeNode(new IdentifierNode("std::optional"), [type]);
        return new ParameterDeclarationNode(
            new IdentifierNode(name),
            optionalType,
            isRequired: false,
            defaultValue: new LiteralExpressionNode("std::nullopt", LiteralKind.String));
    }

    // ===== Тела =====

    private CppFreeFunctionNode BuildConstructorDefinition(string clientClassName)
    {
        var metadata = new Dictionary<string, object> { [ConstructorMetadataKey] = true };
        return new CppFreeFunctionNode(
            new IdentifierNode($"{clientClassName}::{clientClassName}"),
            new RawTypeNode(string.Empty),
            [new CppParameterNode(new IdentifierNode("config"), new RawTypeNode("const ValIO::HttpConfig&"))],
            body: new BlockNode(
            [
                new ExpressionStatementNode(new BinaryExpressionNode(
                    Ident("_http"),
                    BinaryOperator.Assign,
                    new CallExpressionNode(
                        Scope("ValIO", "HttpClient"),
                        [Ident("config")]),
                    isAssignment: true)),
            ]),
            metadata: metadata);
    }

    private CppFreeFunctionNode BuildMethodDefinition(
        string clientClassName, IrOperation operation, IReadOnlySet<string> enumNames, IReadOnlyDictionary<string, IrModel> modelLookup, bool isAsync)
    {
        var methodName = _naming.MethodName(operation.MethodName);
        var name = isAsync ? $"{methodName}Async" : methodName;
        var successType = GetSuccessType(operation);
        var valueTypeName = successType == null
            ? "void"
            : StdTypeText.Render(_typeMapper.MapType(successType));
        _ = modelLookup;

        if (isAsync)
        {
            var captures = new List<string> { "this" };
            captures.AddRange(operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters).Select(p => _naming.PropertyName(p.Name)));
            var hasBody = operation.RequestBody != null;
            if (hasBody)
                captures.Add("request");

            // Порядок аргументов повторяет переупорядоченную сигнатуру:
            // обязательные (path + required query/header + body) вперёд, optional — в хвост.
            var orderedParameters = operation.PathParameters
                .Concat(operation.QueryParameters).Concat(operation.HeaderParameters)
                .OrderBy(p => p.IsRequired ? 0 : 1).ToList();
            var callArguments = new List<ExpressionNode>();
            var bodyInserted = false;
            foreach (var p in orderedParameters)
            {
                if (!p.IsRequired && hasBody && !bodyInserted)
                {
                    callArguments.Add(Ident("request"));
                    bodyInserted = true;
                }
                callArguments.Add(Ident(_naming.PropertyName(p.Name)));
            }
            if (hasBody && !bodyInserted)
                callArguments.Add(Ident("request"));

            return new CppFreeFunctionNode(
                new IdentifierNode($"{clientClassName}::{name}"),
                new GenericTypeNode(new IdentifierNode("std::future"),
                [
                    new GenericTypeNode(new IdentifierNode("ValIO::Expected"),
                        [new RawTypeNode(valueTypeName), new SimpleTypeNode(new IdentifierNode("ValIO::Error"))]),
                ]),
                BuildCppParameters(operation),
                body: new BlockNode(
                [
                    new ReturnStatementNode(new CallExpressionNode(
                        Scope("std", "async"),
                        [
                            Scope("std", "launch", "async"),
                            new CppLambdaExpressionNode(
                                captures,
                                [],
                                new BlockNode([new ReturnStatementNode(new CallExpressionNode(Ident(methodName), callArguments))])),
                        ])),
                ]),
                isConst: true);
        }

        return new CppFreeFunctionNode(
            new IdentifierNode($"{clientClassName}::{name}"),
            new RawTypeNode($"ValIO::Expected<{valueTypeName}, ValIO::Error>"),
            BuildCppParameters(operation),
            body: new BlockNode(BuildSyncBodyStatements(operation, enumNames, modelLookup, valueTypeName)),
            isConst: true);
    }

    /// <summary>Тело sync-метода с именованными локалями: path / httpRequest / body / sendResult / response.</summary>
    private List<StatementNode> BuildSyncBodyStatements(IrOperation operation, IReadOnlySet<string> enumNames, IReadOnlyDictionary<string, IrModel> modelLookup, string valueTypeName)
    {
        var statements = new List<StatementNode>();

        // 1. path — литерал + UrlEncode(path-параметры)
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("path"),
            new SimpleTypeNode(new IdentifierNode("std::string")),
            BuildPathExpression(operation, enumNames)));

        // 2. httpRequest
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("httpRequest"),
            new SimpleTypeNode(new IdentifierNode("ValIO::Request"))));

        // 3. Verb
        statements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
            new MemberAccessExpressionNode(Ident("httpRequest"), new IdentifierNode("Verb")),
            BinaryOperator.Assign,
            Scope("ValIO", "HttpVerb", MapHttpVerb(operation.HttpMethod)),
            isAssignment: true)));

        // 4. Path
        statements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
            new MemberAccessExpressionNode(Ident("httpRequest"), new IdentifierNode("Path")),
            BinaryOperator.Assign,
            Ident("path"),
            isAssignment: true)));

        // 5. query/header параметры (wire-имена — OriginalName)
        foreach (var parameter in operation.QueryParameters)
            AppendParameterCall(statements, parameter, "AddQuery", enumNames);
        foreach (var parameter in operation.HeaderParameters)
            AppendParameterCall(statements, parameter, "AddHeader", enumNames);

        // 6. тело
        AppendRequestBody(statements, operation, modelLookup);

        // 7. sendResult
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("sendResult"),
            new RawTypeNode("const ValIO::Expected<ValIO::Response, ValIO::Error>"),
            new CallExpressionNode(
                new MemberAccessExpressionNode(Ident("_http"), new IdentifierNode("Send")),
                [Ident("httpRequest")])));

        // 8. транспортная ошибка
        statements.Add(new IfStatementNode(
            new UnaryExpressionNode(UnaryOperator.LogicalNot, Ident("sendResult")),
            new BlockNode(
            [
                new ReturnStatementNode(BuildMakeErrorCall(valueTypeName,
                    new CallExpressionNode(new MemberAccessExpressionNode(Ident("sendResult"), new IdentifierNode("error"))))),
            ])));

        // 9. response
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("response"),
            new RawTypeNode("const ValIO::Response&"),
            new CallExpressionNode(new MemberAccessExpressionNode(Ident("sendResult"), new IdentifierNode("value")))));

        // 10. не-2xx
        statements.Add(new IfStatementNode(
            new BinaryExpressionNode(
                new BinaryExpressionNode(
                    new MemberAccessExpressionNode(Ident("response"), new IdentifierNode("StatusCode")),
                    BinaryOperator.LessThan,
                    new LiteralExpressionNode("200", LiteralKind.Number)),
                BinaryOperator.LogicalOr,
                new BinaryExpressionNode(
                    new MemberAccessExpressionNode(Ident("response"), new IdentifierNode("StatusCode")),
                    BinaryOperator.GreaterThanOrEqual,
                    new LiteralExpressionNode("300", LiteralKind.Number))),
            new BlockNode(
            [
                new ReturnStatementNode(BuildMakeErrorCall(valueTypeName,
                    new CallExpressionNode(
                        Scope("ValIO", "ErrorFromStatus"),
                        [
                            new MemberAccessExpressionNode(Ident("response"), new IdentifierNode("StatusCode")),
                            new MemberAccessExpressionNode(Ident("response"), new IdentifierNode("Body")),
                        ]))),
            ])));

        // 11. успех
        statements.Add(valueTypeName == "void"
            ? new ReturnStatementNode(Ident("{}"))
            : new ReturnStatementNode(new CallExpressionNode(
                Scope("ValIO", "Parse"),
                [new MemberAccessExpressionNode(Ident("response"), new IdentifierNode("Body"))],
                genericTypeName: valueTypeName)));

        return statements;
    }

    private ExpressionNode BuildPathExpression(IrOperation operation, IReadOnlySet<string> enumNames)
    {
        _ = enumNames;
        var template = operation.Path;
        var parameters = operation.PathParameters.ToList();
        var parameterIndex = 0;
        ExpressionNode? path = null;

        while (parameterIndex < parameters.Count && template.Contains('{'))
        {
            var placeholderStart = template.IndexOf('{');
            var placeholderEnd = template.IndexOf('}');
            var literalPart = template[..placeholderStart];
            template = template[(placeholderEnd + 1)..];
            var parameter = parameters[parameterIndex++];

            if (literalPart.Length > 0)
            {
                var literal = new LiteralExpressionNode(literalPart, LiteralKind.QuotedString);
                path = path == null ? literal : new BinaryExpressionNode(path, BinaryOperator.Add, literal);
            }

            var encoded = new CallExpressionNode(
                Scope("ValIO", "UrlEncode"),
                [Ident(_typeMapper.FormatQueryLiteral(parameter.Type, _naming.PropertyName(parameter.Name)))]);
            path = path == null ? encoded : new BinaryExpressionNode(path, BinaryOperator.Add, encoded);
        }

        if (template.Length > 0)
        {
            var tail = new LiteralExpressionNode(template, LiteralKind.QuotedString);
            path = path == null ? tail : new BinaryExpressionNode(path, BinaryOperator.Add, tail);
        }

        return path ?? new LiteralExpressionNode(operation.Path, LiteralKind.QuotedString);
    }

    private void AppendParameterCall(List<StatementNode> statements, IrParameter parameter, string addMethod, IReadOnlySet<string> enumNames)
    {
        var name = _naming.PropertyName(parameter.Name);
        var value = _typeMapper.FormatQueryLiteral(parameter.Type, name);

        var call = new ExpressionStatementNode(new CallExpressionNode(
            new MemberAccessExpressionNode(Ident("httpRequest"), new IdentifierNode(addMethod)),
            [
                new LiteralExpressionNode(parameter.OriginalName, LiteralKind.QuotedString),
                Ident(value),
            ]));

        if (parameter.IsRequired)
        {
            statements.Add(call);
        }
        else
        {
            statements.Add(new IfStatementNode(
                Ident(name),
                new BlockNode(
                [
                    new ExpressionStatementNode(new CallExpressionNode(
                        new MemberAccessExpressionNode(Ident("httpRequest"), new IdentifierNode(addMethod)),
                        [
                            new LiteralExpressionNode(parameter.OriginalName, LiteralKind.QuotedString),
                            Ident(_typeMapper.FormatQueryLiteral(parameter.Type, $"*{name}")),
                        ])),
                ])));
        }
    }

    private void AppendRequestBody(List<StatementNode> statements, IrOperation operation, IReadOnlyDictionary<string, IrModel> modelLookup)
    {
        var body = operation.RequestBody;
        if (body == null)
        {
            return;
        }

        if (body.IsJson)
        {
            // const std::string body = ValIO::Dump(request); httpRequest.SetJsonBody(body);
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("body"),
                new SimpleTypeNode(new IdentifierNode("std::string")),
                new CallExpressionNode(
                    Scope("ValIO", "Dump"),
                    [Ident("request")])));
            statements.Add(new ExpressionStatementNode(new CallExpressionNode(
                new MemberAccessExpressionNode(Ident("httpRequest"), new IdentifierNode("SetJsonBody")),
                [Ident("body")])));
        }
        else if (body.IsFormUrlEncoded)
        {
            foreach (var property in GetBodyProperties(body, modelLookup))
            {
                var literal = _typeMapper.FormatQueryLiteral(property.Type, $"request.{_naming.PropertyName(property.Name)}");
                statements.Add(new ExpressionStatementNode(new CallExpressionNode(
                    new MemberAccessExpressionNode(Ident("httpRequest"), new IdentifierNode("AddFormField")),
                    [
                        new LiteralExpressionNode(property.OriginalName, LiteralKind.QuotedString),
                        Ident(literal),
                    ])));
            }
        }
        else if (body.IsMultipartFormData)
        {
            foreach (var property in GetBodyProperties(body, modelLookup))
            {
                var fieldName = $"request.{_naming.PropertyName(property.Name)}";
                if (IrTypeQueries.IsFileOrBinary(property.Type))
                {
                    statements.Add(new ExpressionStatementNode(new CallExpressionNode(
                        new MemberAccessExpressionNode(Ident("httpRequest"), new IdentifierNode("AddFilePart")),
                        [
                            new LiteralExpressionNode(property.OriginalName, LiteralKind.QuotedString),
                            new LiteralExpressionNode(property.OriginalName, LiteralKind.QuotedString),
                            Ident(fieldName),
                            new LiteralExpressionNode("application/octet-stream", LiteralKind.QuotedString),
                        ])));
                }
                else
                {
                    var literal = _typeMapper.FormatQueryLiteral(property.Type, fieldName);
                    statements.Add(new ExpressionStatementNode(new CallExpressionNode(
                        new MemberAccessExpressionNode(Ident("httpRequest"), new IdentifierNode("AddFormField")),
                        [
                            new LiteralExpressionNode(property.OriginalName, LiteralKind.QuotedString),
                            Ident(literal),
                        ])));
                }
            }
        }
    }

    private static IEnumerable<IrProperty> GetBodyProperties(IrRequestBody body, IReadOnlyDictionary<string, IrModel> modelLookup)
    {
        return body.Type is IrModelReference modelRef && modelLookup.TryGetValue(modelRef.ModelName, out var model)
            ? model.Properties
            : [];
    }

    private static CallExpressionNode BuildMakeErrorCall(string valueTypeName, ExpressionNode errorExpression)
        => new(
            Scope("ValIO", "MakeError"),
            [errorExpression],
            genericTypeName: valueTypeName);

    private static IrType? GetSuccessType(IrOperation operation)
        => operation.Responses.FirstOrDefault(r => IsSuccessStatusCode(r.StatusCode))?.Type
           ?? operation.Responses.FirstOrDefault(r => r.StatusCode == "default")?.Type;

    private static bool IsSuccessStatusCode(string statusCode)
        => statusCode.Length == 3 && statusCode[0] == '2';

    private List<CppParameterNode> BuildCppParameters(IrOperation operation)
        => BuildMethodParameters(operation)
            .Select(p => new CppParameterNode((IdentifierNode)p.Name.Clone(), p.Type!))
            .ToList();

    private static string MapHttpVerb(string httpMethod) => httpMethod.Trim().ToUpperInvariant() switch
    {
        "GET" => "Get",
        "POST" => "Post",
        "PUT" => "Put",
        "DELETE" => "Delete",
        "PATCH" => "Patch",
        "HEAD" => "Head",
        "OPTIONS" => "Options",
        _ => "Get",
    };

    private void CollectOperationReferences(IrOperation operation, ISet<string> models, ISet<string> enums, IReadOnlySet<string> enumNames)
    {
        foreach (var parameter in operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters))
            StdTypeReferenceCollector.Collect(parameter.Type, models, enums, enumNames);
        if (operation.RequestBody != null && !IrTypeQueries.IsFileOrBinary(operation.RequestBody.Type))
            StdTypeReferenceCollector.Collect(operation.RequestBody.Type, models, enums, enumNames);
        foreach (var response in operation.Responses)
        {
            if (response.Type != null)
                StdTypeReferenceCollector.Collect(response.Type, models, enums, enumNames);
        }
    }

    private List<CppIncludeDirectiveNode> BuildInterfaceIncludes(
        HashSet<string> referencedModels, HashSet<string> referencedEnums, IReadOnlySet<string> enumNames, bool emitAsync)
    {
        var includes = new List<CppIncludeDirectiveNode>();
        if (emitAsync)
            includes.Add(new CppIncludeDirectiveNode("future", isSystemInclude: true));
        includes.Add(new CppIncludeDirectiveNode("Runtime/ValIOError.h"));
        includes.Add(new CppIncludeDirectiveNode("Runtime/ValIOExpected.h"));
        includes.AddRange(BuildModelIncludes(referencedModels, referencedEnums));
        return includes;
    }

    private List<CppIncludeDirectiveNode> BuildClientIncludes(
        string clientName, HashSet<string> referencedModels, HashSet<string> referencedEnums, IReadOnlySet<string> enumNames, bool useInterface, bool emitAsync)
    {
        var includes = new List<CppIncludeDirectiveNode>();
        if (emitAsync)
            includes.Add(new CppIncludeDirectiveNode("future", isSystemInclude: true));
        if (useInterface)
            includes.Add(new CppIncludeDirectiveNode(_naming.InterfaceHeaderPath(clientName)));
        includes.Add(new CppIncludeDirectiveNode("Runtime/ValIOError.h"));
        includes.Add(new CppIncludeDirectiveNode("Runtime/ValIOExpected.h"));
        includes.Add(new CppIncludeDirectiveNode("Runtime/ValIOHttp.h"));
        includes.AddRange(BuildModelIncludes(referencedModels, referencedEnums));
        return includes;
    }

    private List<CppIncludeDirectiveNode> BuildSourceIncludes(
        string clientName, HashSet<string> referencedModels, HashSet<string> referencedEnums, IReadOnlySet<string> enumNames, bool useInterface, CppGenerationOptions options)
    {
        var includes = new List<CppIncludeDirectiveNode>
        {
            new(_naming.ClientHeaderPath(clientName)),
            new("Runtime/ValIOJson.h"),
        };
        foreach (var refModel in referencedModels.OrderBy(x => x, StringComparer.Ordinal))
            includes.Add(new CppIncludeDirectiveNode(_naming.SerializerHeaderPath(refModel)));
        foreach (var refEnum in referencedEnums.OrderBy(x => x, StringComparer.Ordinal))
            includes.Add(new CppIncludeDirectiveNode(_naming.SerializerHeaderPath(refEnum)));
        return includes;
    }

    private IEnumerable<CppIncludeDirectiveNode> BuildModelIncludes(HashSet<string> referencedModels, HashSet<string> referencedEnums)
    {
        foreach (var refModel in referencedModels.OrderBy(x => x, StringComparer.Ordinal))
            yield return new CppIncludeDirectiveNode(_naming.ModelHeaderPath(refModel));
        foreach (var refEnum in referencedEnums.OrderBy(x => x, StringComparer.Ordinal))
            yield return new CppIncludeDirectiveNode(_naming.EnumHeaderPath(refEnum));
    }

    private static IdentifierExpressionNode Ident(string name) => new(new IdentifierNode(name));

    /// <summary>A::B::C — цепочка scope-доступа.</summary>
    private static ExpressionNode Scope(params string[] parts)
    {
        ExpressionNode current = Ident(parts[0]);
        for (var i = 1; i < parts.Length; i++)
            current = new MemberAccessExpressionNode(current, new IdentifierNode(parts[i]), useScopeOperator: true);
        return current;
    }
}
