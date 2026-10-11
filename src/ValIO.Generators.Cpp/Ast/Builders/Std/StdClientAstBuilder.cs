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

    /// <summary>Builds a client and its optional interface for the selected execution style.</summary>
    /// <param name="client">The client contract.</param>
    /// <param name="enumNames">The document's enum names.</param>
    /// <param name="modelLookup">The document's model contracts.</param>
    /// <param name="rootNamespace">The generated namespace.</param>
    /// <param name="options">The client output options.</param>
    /// <returns>The client declarations and implementation.</returns>
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
        var originalNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var standardIncludes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var operation in client.Operations)
        {
            CollectOperationReferences(operation, referencedModels, referencedEnums, enumNames);
            foreach (var type in OperationTypes(operation))
            {
                CppTypeReferenceCollector.CollectOriginalNames(type, originalNames);
                foreach (var include in StdCppTypeMapper.StdIncludesFor(type))
                    standardIncludes.Add(include);
            }
            if (operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters).Any(p => !p.IsRequired))
                standardIncludes.Add("<optional>");
        }

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
                includes: BuildInterfaceIncludes(referencedModels, referencedEnums, originalNames, standardIncludes, emitAsync),
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
            if (emitSync || emitAsync)
                classMembers.Add(BuildMethodDeclaration(operation, isAsync: false, isInterface: false,
                    accessModifier: emitSync ? AccessModifier.Public : AccessModifier.Private));
            if (emitAsync)
                classMembers.Add(BuildMethodDeclaration(operation, isAsync: true, isInterface: false));
        }

        classMembers.Add(new FieldDeclarationNode(
            new IdentifierNode("_config"),
            new SimpleTypeNode(new IdentifierNode("ValIO::HttpConfig")),
            AccessModifier.Private));
        classMembers.Add(new FieldDeclarationNode(
            new IdentifierNode("_http"),
            new SimpleTypeNode(new IdentifierNode("ValIO::HttpClient")),
            AccessModifier.Private));

        var clientHeader = new CppHeaderFileNode(
            fileName: _naming.ClientHeaderPath(client.Name),
            includes: BuildClientIncludes(client.Name, referencedModels, referencedEnums, originalNames, standardIncludes, useInterface, emitAsync),
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
            if (emitSync || emitAsync)
                sourceMembers.Add(BuildMethodDefinition(clientClassName, operation, enumNames, modelLookup, isAsync: false));
            if (emitAsync)
                sourceMembers.Add(BuildMethodDefinition(clientClassName, operation, enumNames, modelLookup, isAsync: true));
        }

        var clientSource = new CppSourceFileNode(
            fileName: _naming.ClientSourcePath(client.Name),
            includes: BuildSourceIncludes(client.Name, referencedModels, referencedEnums, originalNames),
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

    /// <summary>Declares an operation or an internal synchronous implementation for async-only clients.</summary>
    /// <param name="operation">The operation contract.</param>
    /// <param name="isAsync">Whether this declaration returns a future.</param>
    /// <param name="isInterface">Whether it declares a pure virtual interface method.</param>
    /// <param name="accessModifier">The visibility of the implementation method.</param>
    /// <returns>The operation declaration.</returns>
    private MethodDeclarationNode BuildMethodDeclaration(
        IrOperation operation, bool isAsync, bool isInterface, AccessModifier accessModifier = AccessModifier.Public)
    {
        var methodName = _naming.MethodName(operation.MethodName);
        return new MethodDeclarationNode(
            new IdentifierNode(isAsync ? $"{methodName}Async" : methodName),
            returnType: BuildExpectedType(GetSuccessType(operation), isAsync),
            parameters: BuildMethodParameters(operation),
            accessModifier: accessModifier,
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
            defaultValue: new IdentifierExpressionNode(new IdentifierNode("std::nullopt")));
    }

    // ===== Тела =====

    /// <summary>Initializes the HTTP transport and retains configuration for independently owned async work.</summary>
    /// <param name="clientClassName">The generated client type.</param>
    /// <returns>The out-of-class constructor definition.</returns>
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
                    StdExpressions.Ident("_config"), BinaryOperator.Assign, StdExpressions.Ident("config"), isAssignment: true)),
                new ExpressionStatementNode(new BinaryExpressionNode(
                    StdExpressions.Ident("_http"),
                    BinaryOperator.Assign,
                    new CallExpressionNode(
                        StdExpressions.Scope("ValIO", "HttpClient"),
                        [StdExpressions.Ident("config")]),
                    isAssignment: true)),
            ]),
            metadata: metadata);
    }

    /// <summary>Defines an operation or an async wrapper owning its transport and parameter values.</summary>
    /// <param name="clientClassName">The generated client type.</param>
    /// <param name="operation">The operation contract.</param>
    /// <param name="enumNames">The document's enum names.</param>
    /// <param name="modelLookup">The available request model contracts.</param>
    /// <param name="isAsync">Whether this definition returns a future.</param>
    /// <returns>The operation definition.</returns>
    private CppFreeFunctionNode BuildMethodDefinition(
        string clientClassName, IrOperation operation, IReadOnlySet<string> enumNames, IReadOnlyDictionary<string, IrModel> modelLookup, bool isAsync)
    {
        var methodName = _naming.MethodName(operation.MethodName);
        var name = isAsync ? $"{methodName}Async" : methodName;
        var successType = GetSuccessType(operation);
        var valueTypeName = successType == null
            ? "void"
            : StdTypeText.Render(_typeMapper.MapType(successType));

        if (isAsync)
        {
            var captures = new List<string> { "httpConfig = _config" };
            captures.AddRange(operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters).Select(p => _naming.PropertyName(p.Name)));
            var hasBody = operation.RequestBody != null;
            if (hasBody)
                captures.Add("request");

            // Required parameters and the body precede all trailing default arguments.
            var orderedParameters = operation.PathParameters
                .Concat(operation.QueryParameters).Concat(operation.HeaderParameters)
                .OrderBy(p => p.IsRequired ? 0 : 1).ToList();
            var callArguments = new List<ExpressionNode>();
            var bodyInserted = false;
            foreach (var p in orderedParameters)
            {
                if (!p.IsRequired && hasBody && !bodyInserted)
                {
                    callArguments.Add(StdExpressions.Ident("request"));
                    bodyInserted = true;
                }
                callArguments.Add(StdExpressions.Ident(_naming.PropertyName(p.Name)));
            }
            if (hasBody && !bodyInserted)
                callArguments.Add(StdExpressions.Ident("request"));

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
                        StdExpressions.Scope("std", "async"),
                        [
                            StdExpressions.Scope("std", "launch", "async"),
                            new CppLambdaExpressionNode(
                                captures,
                                [],
                                new BlockNode(
                                [
                                    new VariableDeclarationStatementNode(new IdentifierNode("worker"), new RawTypeNode(clientClassName),
                                        new CallExpressionNode(StdExpressions.Ident(clientClassName), [StdExpressions.Ident("httpConfig")])),
                                    new ReturnStatementNode(new CallExpressionNode(
                                        new MemberAccessExpressionNode(StdExpressions.Ident("worker"), new IdentifierNode(methodName)), callArguments)),
                                ])),
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
            BuildPathExpression(operation)));

        // 2. httpRequest
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("httpRequest"),
            new SimpleTypeNode(new IdentifierNode("ValIO::Request"))));

        // 3. Verb
        statements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
            new MemberAccessExpressionNode(StdExpressions.Ident("httpRequest"), new IdentifierNode("Verb")),
            BinaryOperator.Assign,
            StdExpressions.Scope("ValIO", "HttpVerb", MapHttpVerb(operation.HttpMethod)),
            isAssignment: true)));

        // 4. Path
        statements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
            new MemberAccessExpressionNode(StdExpressions.Ident("httpRequest"), new IdentifierNode("Path")),
            BinaryOperator.Assign,
            StdExpressions.Ident("path"),
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
                new MemberAccessExpressionNode(StdExpressions.Ident("_http"), new IdentifierNode("Send")),
                [StdExpressions.Ident("httpRequest")])));

        // 8. транспортная ошибка
        statements.Add(new IfStatementNode(
            new UnaryExpressionNode(UnaryOperator.LogicalNot, StdExpressions.Ident("sendResult")),
            new BlockNode(
            [
                new ReturnStatementNode(BuildMakeErrorCall(valueTypeName,
                    new CallExpressionNode(new MemberAccessExpressionNode(StdExpressions.Ident("sendResult"), new IdentifierNode("error"))))),
            ])));

        // 9. response
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("response"),
            new RawTypeNode("const ValIO::Response&"),
            new CallExpressionNode(new MemberAccessExpressionNode(StdExpressions.Ident("sendResult"), new IdentifierNode("value")))));

        // 10. не-2xx
        statements.Add(new IfStatementNode(
            new BinaryExpressionNode(
                new BinaryExpressionNode(
                    new MemberAccessExpressionNode(StdExpressions.Ident("response"), new IdentifierNode("StatusCode")),
                    BinaryOperator.LessThan,
                    new LiteralExpressionNode("200", LiteralKind.Number)),
                BinaryOperator.LogicalOr,
                new BinaryExpressionNode(
                    new MemberAccessExpressionNode(StdExpressions.Ident("response"), new IdentifierNode("StatusCode")),
                    BinaryOperator.GreaterThanOrEqual,
                    new LiteralExpressionNode("300", LiteralKind.Number))),
            new BlockNode(
            [
                new ReturnStatementNode(BuildMakeErrorCall(valueTypeName,
                    new CallExpressionNode(
                        StdExpressions.Scope("ValIO", "ErrorFromStatus"),
                        [
                            new MemberAccessExpressionNode(StdExpressions.Ident("response"), new IdentifierNode("StatusCode")),
                            new MemberAccessExpressionNode(StdExpressions.Ident("response"), new IdentifierNode("Body")),
                        ]))),
            ])));

        // 11. успех
        statements.Add(valueTypeName == "void"
            ? new ReturnStatementNode(StdExpressions.Ident("{}"))
            : new ReturnStatementNode(new CallExpressionNode(
                StdExpressions.Scope("ValIO", "Parse"),
                [new MemberAccessExpressionNode(StdExpressions.Ident("response"), new IdentifierNode("Body"))],
                genericTypeName: valueTypeName)));

        return statements;
    }

    /// <summary>Matches path placeholders by their original schema names rather than parameter declaration order.</summary>
    /// <param name="operation">The operation containing the path template.</param>
    /// <returns>The encoded path expression.</returns>
    /// <exception cref="InvalidOperationException">A placeholder is malformed or has no declared parameter.</exception>
    private ExpressionNode BuildPathExpression(IrOperation operation)
    {
        var template = operation.Path;
        ExpressionNode? path = null;

        while (template.Contains('{'))
        {
            var placeholderStart = template.IndexOf('{');
            var placeholderEnd = template.IndexOf('}', placeholderStart + 1);
            if (placeholderEnd < 0)
                throw new InvalidOperationException($"Operation '{operation.MethodName}' has an unterminated path placeholder.");
            var placeholder = template[(placeholderStart + 1)..placeholderEnd];
            var parameter = operation.PathParameters.FirstOrDefault(p => p.OriginalName == placeholder)
                ?? throw new InvalidOperationException($"Operation '{operation.MethodName}' has no path parameter for '{{{placeholder}}}'.");
            var literalPart = template[..placeholderStart];
            template = template[(placeholderEnd + 1)..];

            if (literalPart.Length > 0)
            {
                var literal = new LiteralExpressionNode(literalPart, LiteralKind.String);
                path = path == null ? literal : new BinaryExpressionNode(path, BinaryOperator.Add, literal);
            }

            var encoded = new CallExpressionNode(
                StdExpressions.Scope("ValIO", "UrlEncode"),
                [StdExpressions.Ident(_typeMapper.FormatQueryLiteral(parameter.Type, _naming.PropertyName(parameter.Name)))]);
            path = path == null ? encoded : new BinaryExpressionNode(path, BinaryOperator.Add, encoded);
        }

        if (template.Length > 0)
        {
            var tail = new LiteralExpressionNode(template, LiteralKind.String);
            path = path == null ? tail : new BinaryExpressionNode(path, BinaryOperator.Add, tail);
        }

        return path ?? new LiteralExpressionNode(operation.Path, LiteralKind.String);
    }

    private void AppendParameterCall(List<StatementNode> statements, IrParameter parameter, string addMethod, IReadOnlySet<string> enumNames)
    {
        var name = _naming.PropertyName(parameter.Name);
        var value = _typeMapper.FormatQueryLiteral(parameter.Type, name);

        var call = new ExpressionStatementNode(new CallExpressionNode(
            new MemberAccessExpressionNode(StdExpressions.Ident("httpRequest"), new IdentifierNode(addMethod)),
            [
                new LiteralExpressionNode(parameter.OriginalName, LiteralKind.String),
                StdExpressions.Ident(value),
            ]));

        if (parameter.IsRequired)
        {
            statements.Add(call);
        }
        else
        {
            statements.Add(new IfStatementNode(
                StdExpressions.Ident(name),
                new BlockNode(
                [
                    new ExpressionStatementNode(new CallExpressionNode(
                        new MemberAccessExpressionNode(StdExpressions.Ident("httpRequest"), new IdentifierNode(addMethod)),
                        [
                            new LiteralExpressionNode(parameter.OriginalName, LiteralKind.String),
                            StdExpressions.Ident(_typeMapper.FormatQueryLiteral(parameter.Type, $"*{name}")),
                        ])),
                ])));
        }
    }

    /// <summary>Builds JSON or form request content while preserving optional field presence.</summary>
    /// <param name="statements">The operation statements being constructed.</param>
    /// <param name="operation">The operation contract.</param>
    /// <param name="modelLookup">The available request model contracts.</param>
    private void AppendRequestBody(List<StatementNode> statements, IrOperation operation, IReadOnlyDictionary<string, IrModel> modelLookup)
    {
        var body = operation.RequestBody;
        if (body == null)
        {
            return;
        }

        if (body.IsJson)
        {
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("body"),
                new SimpleTypeNode(new IdentifierNode("std::string")),
                new CallExpressionNode(
                    StdExpressions.Scope("ValIO", "Dump"),
                    [StdExpressions.Ident("request")])));
            statements.Add(new ExpressionStatementNode(new CallExpressionNode(
                new MemberAccessExpressionNode(StdExpressions.Ident("httpRequest"), new IdentifierNode("SetJsonBody")),
                [StdExpressions.Ident("body")])));
        }
        else if (body.IsFormUrlEncoded || body.IsMultipartFormData)
        {
            if (body.IsMultipartFormData)
                statements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
                    new MemberAccessExpressionNode(StdExpressions.Ident("httpRequest"), new IdentifierNode("ContentType")),
                    BinaryOperator.Assign, new LiteralExpressionNode("multipart/form-data", LiteralKind.String), isAssignment: true)));
            foreach (var property in GetBodyProperties(body, modelLookup))
                statements.Add(BuildFormProperty(property, body.IsMultipartFormData));
        }
    }

    /// <summary>Builds one form field or binary part and omits absent optional fields.</summary>
    /// <param name="property">The request model property.</param>
    /// <param name="isMultipart">Whether binary values are emitted as file parts.</param>
    /// <returns>The field conversion and its optional presence guard.</returns>
    private StatementNode BuildFormProperty(IrProperty property, bool isMultipart)
    {
        var field = $"request.{_naming.PropertyName(property.Name)}";
        var isOptional = StdPropertyTypes.IsOptional(property);
        var value = isOptional ? $"(*{field})" : field;
        var type = StdPropertyTypes.ValueType(property);
        var isBinary = isMultipart && IrTypeQueries.IsFileOrBinary(type);
        var nullable = type as IrNullableType;
        var concreteValue = nullable == null ? value : $"(*{value})";
        var concreteType = nullable?.InnerType ?? type;

        StatementNode statement;
        if (isBinary)
        {
            var fileValue = concreteType is IrArrayType ? "filePart" : concreteValue;
            statement = new ExpressionStatementNode(new CallExpressionNode(
                new MemberAccessExpressionNode(StdExpressions.Ident("httpRequest"), new IdentifierNode("AddFilePart")),
                [
                    new LiteralExpressionNode(property.OriginalName, LiteralKind.String),
                    new LiteralExpressionNode(property.OriginalName, LiteralKind.String),
                    StdExpressions.Ident(fileValue),
                    new LiteralExpressionNode("application/octet-stream", LiteralKind.String),
                ]));
            if (concreteType is IrArrayType)
                statement = new ForEachStatementNode(new IdentifierNode("filePart"), StdExpressions.Ident(concreteValue), new BlockNode([statement]));
            if (nullable != null)
                statement = new IfStatementNode(StdExpressions.Ident(value), new BlockNode([statement]));
        }
        else
        {
            var literal = _typeMapper.FormatQueryLiteral(concreteType, concreteValue);
            var wireValue = nullable == null ? literal : $"({value} ? {literal} : \"null\")";
            statement = new ExpressionStatementNode(new CallExpressionNode(
                new MemberAccessExpressionNode(StdExpressions.Ident("httpRequest"), new IdentifierNode("AddFormField")),
                [new LiteralExpressionNode(property.OriginalName, LiteralKind.String), StdExpressions.Ident(wireValue)]));
        }

        return isOptional ? new IfStatementNode(StdExpressions.Ident(field), new BlockNode([statement])) : statement;
    }

    private static IEnumerable<IrProperty> GetBodyProperties(IrRequestBody body, IReadOnlyDictionary<string, IrModel> modelLookup)
    {
        return body.Type is IrModelReference modelRef && modelLookup.TryGetValue(modelRef.ModelName, out var model)
            ? model.Properties
            : [];
    }

    private static CallExpressionNode BuildMakeErrorCall(string valueTypeName, ExpressionNode errorExpression)
        => new(
            StdExpressions.Scope("ValIO", "MakeError"),
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
            CppTypeReferenceCollector.Collect(parameter.Type, models, enums, enumNames);
        if (operation.RequestBody != null && !IrTypeQueries.IsFileOrBinary(operation.RequestBody.Type))
            CppTypeReferenceCollector.Collect(operation.RequestBody.Type, models, enums, enumNames);
        foreach (var response in operation.Responses)
        {
            if (response.Type != null)
                CppTypeReferenceCollector.Collect(response.Type, models, enums, enumNames);
        }
    }

    /// <summary>Enumerates every type used by an operation for include and original-name resolution.</summary>
    /// <param name="operation">The operation contract.</param>
    /// <returns>The parameter, body, and response types.</returns>
    private static IEnumerable<IrType> OperationTypes(IrOperation operation)
    {
        foreach (var parameter in operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters))
            yield return parameter.Type;
        if (operation.RequestBody != null)
            yield return operation.RequestBody.Type;
        foreach (var response in operation.Responses)
            if (response.Type != null)
                yield return response.Type;
    }

    /// <summary>Builds self-contained interface includes without depending on model transitive includes.</summary>
    /// <param name="referencedModels">The referenced model names.</param>
    /// <param name="referencedEnums">The referenced enum names.</param>
    /// <param name="originalNames">The stable source names used for file paths.</param>
    /// <param name="standardIncludes">The standard headers required by all operation types.</param>
    /// <param name="emitAsync">Whether future declarations are generated.</param>
    /// <returns>The interface include directives.</returns>
    private List<CppIncludeDirectiveNode> BuildInterfaceIncludes(
        HashSet<string> referencedModels, HashSet<string> referencedEnums, IReadOnlyDictionary<string, string> originalNames,
        IEnumerable<string> standardIncludes, bool emitAsync)
    {
        var includes = standardIncludes.Select(i => new CppIncludeDirectiveNode(i.Trim('<', '>'), isSystemInclude: true)).ToList();
        if (emitAsync)
            includes.Add(new CppIncludeDirectiveNode("future", isSystemInclude: true));
        includes.Add(new CppIncludeDirectiveNode("Runtime/ValIOError.h"));
        includes.Add(new CppIncludeDirectiveNode("Runtime/ValIOExpected.h"));
        includes.AddRange(BuildModelIncludes(referencedModels, referencedEnums, originalNames));
        return includes;
    }

    /// <summary>Builds client includes with explicit standard dependencies and stable schema file names.</summary>
    /// <param name="clientName">The client contract name.</param>
    /// <param name="referencedModels">The referenced model names.</param>
    /// <param name="referencedEnums">The referenced enum names.</param>
    /// <param name="originalNames">The stable source names used for file paths.</param>
    /// <param name="standardIncludes">The standard headers required by all operation types.</param>
    /// <param name="useInterface">Whether the client implements a generated interface.</param>
    /// <param name="emitAsync">Whether future declarations are generated.</param>
    /// <returns>The client include directives.</returns>
    private List<CppIncludeDirectiveNode> BuildClientIncludes(
        string clientName, HashSet<string> referencedModels, HashSet<string> referencedEnums,
        IReadOnlyDictionary<string, string> originalNames, IEnumerable<string> standardIncludes, bool useInterface, bool emitAsync)
    {
        var includes = standardIncludes.Select(i => new CppIncludeDirectiveNode(i.Trim('<', '>'), isSystemInclude: true)).ToList();
        if (emitAsync)
            includes.Add(new CppIncludeDirectiveNode("future", isSystemInclude: true));
        if (useInterface)
            includes.Add(new CppIncludeDirectiveNode(_naming.InterfaceHeaderPath(clientName)));
        includes.Add(new CppIncludeDirectiveNode("Runtime/ValIOError.h"));
        includes.Add(new CppIncludeDirectiveNode("Runtime/ValIOExpected.h"));
        includes.Add(new CppIncludeDirectiveNode("Runtime/ValIOHttp.h"));
        includes.AddRange(BuildModelIncludes(referencedModels, referencedEnums, originalNames));
        return includes;
    }

    /// <summary>Collects codec declarations using original schema names even after identifier affixes.</summary>
    /// <param name="clientName">The client contract name.</param>
    /// <param name="referencedModels">The referenced model names.</param>
    /// <param name="referencedEnums">The referenced enum names.</param>
    /// <param name="originalNames">The stable source names used for file paths.</param>
    /// <returns>The implementation include directives.</returns>
    private List<CppIncludeDirectiveNode> BuildSourceIncludes(
        string clientName, HashSet<string> referencedModels, HashSet<string> referencedEnums, IReadOnlyDictionary<string, string> originalNames)
    {
        var includes = new List<CppIncludeDirectiveNode>
        {
            new(_naming.ClientHeaderPath(clientName)),
            new("Runtime/ValIOJson.h"),
        };
        foreach (var refModel in referencedModels.OrderBy(x => x, StringComparer.Ordinal))
            includes.Add(new CppIncludeDirectiveNode(_naming.SerializerHeaderPath(originalNames.GetValueOrDefault(refModel, refModel))));
        foreach (var refEnum in referencedEnums.OrderBy(x => x, StringComparer.Ordinal))
            includes.Add(new CppIncludeDirectiveNode(_naming.SerializerHeaderPath(originalNames.GetValueOrDefault(refEnum, refEnum))));
        return includes;
    }

    /// <summary>Builds model and enum includes from stable schema names.</summary>
    /// <param name="referencedModels">The referenced model names.</param>
    /// <param name="referencedEnums">The referenced enum names.</param>
    /// <param name="originalNames">The stable source names used for file paths.</param>
    /// <returns>The referenced declaration include directives.</returns>
    private IEnumerable<CppIncludeDirectiveNode> BuildModelIncludes(HashSet<string> referencedModels, HashSet<string> referencedEnums,
        IReadOnlyDictionary<string, string> originalNames)
    {
        foreach (var refModel in referencedModels.OrderBy(x => x, StringComparer.Ordinal))
            yield return new CppIncludeDirectiveNode(_naming.ModelHeaderPath(originalNames.GetValueOrDefault(refModel, refModel)));
        foreach (var refEnum in referencedEnums.OrderBy(x => x, StringComparer.Ordinal))
            yield return new CppIncludeDirectiveNode(_naming.EnumHeaderPath(originalNames.GetValueOrDefault(refEnum, refEnum)));
    }

    }
