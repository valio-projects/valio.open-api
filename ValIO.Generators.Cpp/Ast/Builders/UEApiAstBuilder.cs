using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Nodes.UE;
using ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Ast.Builders;

/// <summary>
/// Строитель AST для API файлов (.h + .cpp) из IrClient.
/// Генерирует: делегаты, native TFuture-функции, Blueprint Async Actions,
/// PathsLibrary, Parse-функции, реализации методов.
/// </summary>
public sealed class UEApiAstBuilder
{
    private readonly UETypeMapper _typeMapper;
    private readonly UENamingConvention _naming;
    private readonly CppGenerationOptions _options;
    private readonly UEResultAstBuilder _resultBuilder;

    public UEApiAstBuilder(
        UETypeMapper typeMapper,
        UENamingConvention naming,
        CppGenerationOptions options,
        UEResultAstBuilder resultBuilder)
    {
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _resultBuilder = resultBuilder ?? throw new ArgumentNullException(nameof(resultBuilder));
    }

    /// <summary>
    /// Строит пару (header, source) файлов для одного endpoint (одна операция).
    /// Файлы помещаются в папку Endpoints/.
    /// </summary>
    public (CppHeaderFileNode Header, CppSourceFileNode Source) BuildEndpoint(IrClient client, IrOperation operation)
    {
        var header = BuildEndpointHeader(client, operation);
        var source = BuildEndpointSource(client, operation);
        return (header, source);
    }

    /// <summary>
    /// Строит пару (header, source) файлов для PathsLibrary клиента.
    /// </summary>
    public (CppHeaderFileNode Header, CppSourceFileNode Source) BuildPaths(IrClient client)
    {
        var header = BuildPathsHeader(client);
        var source = BuildPathsSource(client);
        return (header, source);
    }

    /// <summary>
    /// Строит заголовочный файл (.h) для одного endpoint
    /// </summary>
    private CppHeaderFileNode BuildEndpointHeader(IrClient client, IrOperation operation)
    {
        var headerFileName = _naming.EndpointHeaderFileName(operation.MethodName);
        var declarations = new List<AstNode>
        {
            BuildDelegateDeclaration(operation),
            BuildNativeFunctionDeclaration(client.Name, operation),
            BuildAsyncActionClass(client.Name, operation)
        };

        var includes = BuildEndpointHeaderIncludes(operation, headerFileName);
        var forwardDecls = BuildForwardDeclarations();
        var usingAliases = BuildUsingAliases();

        return new CppHeaderFileNode(
            fileName: headerFileName,
            headerComment: _options.Copyright != null ? new CommentNode(_options.Copyright, CommentKind.MultiLine) : null,
            includes: includes,
            forwardDeclarations: forwardDecls,
            usingAliases: usingAliases,
            declarations: declarations);
    }

    /// <summary>
    /// Строит файл реализации (.cpp) для одного endpoint
    /// </summary>
    private CppSourceFileNode BuildEndpointSource(IrClient client, IrOperation operation)
    {
        var sourceFileName = _naming.EndpointSourceFileName(operation.MethodName);
        var headerFileName = _naming.EndpointHeaderFileName(operation.MethodName);
        var pathsIncludePath = $"../Api/{_naming.PathsHeaderFileName(client.Name)}";

        var includes = new List<CppIncludeDirectiveNode>
        {
            new(headerFileName),
            new(pathsIncludePath),
            new(_options.ValIOClientIncludePath),
            new(_options.ValIORequestBuilderIncludePath),
            new(_options.ValIOMultipartFormDataIncludePath),
            new(_options.ValIOTypesIncludePath),
            new(_options.ValIOResponseIncludePath),
            new("Async/Async.h")
        };

        var anonymousMembers = new List<AstNode> { BuildParseFunction(operation) };
        var implementations = new List<AstNode>();
        implementations.Add(BuildNativeFunctionImplementation(client.Name, operation));
        implementations.AddRange(BuildAsyncActionImplementations(client.Name, operation));

        return new CppSourceFileNode(
            fileName: sourceFileName,
            headerComment: _options.Copyright != null ? new CommentNode(_options.Copyright, CommentKind.MultiLine) : null,
            includes: includes,
            anonymousNamespaceMembers: anonymousMembers,
            implementations: implementations);
    }

    /// <summary>
    /// Строит заголовочный файл PathsLibrary
    /// </summary>
    private CppHeaderFileNode BuildPathsHeader(IrClient client)
    {
        var headerFileName = _naming.PathsHeaderFileName(client.Name);
        var declarations = new List<AstNode> { BuildPathsLibraryClass(client) };

        var includes = new List<CppIncludeDirectiveNode>
        {
            new("CoreMinimal.h"),
            new("Kismet/BlueprintFunctionLibrary.h"),
            new(headerFileName.Replace(".h", _options.GeneratedHeaderSuffix))
        };

        return new CppHeaderFileNode(
            fileName: headerFileName,
            headerComment: _options.Copyright != null ? new CommentNode(_options.Copyright, CommentKind.MultiLine) : null,
            includes: includes,
            forwardDeclarations: ImmutableList<CppForwardDeclarationNode>.Empty,
            usingAliases: ImmutableList<CppUsingAliasNode>.Empty,
            declarations: declarations);
    }

    /// <summary>
    /// Строит файл реализации PathsLibrary
    /// </summary>
    private CppSourceFileNode BuildPathsSource(IrClient client)
    {
        var sourceFileName = _naming.PathsSourceFileName(client.Name);
        var headerFileName = _naming.PathsHeaderFileName(client.Name);

        var includes = new List<CppIncludeDirectiveNode>
        {
            new(headerFileName),
            new(_options.ValIOTypesIncludePath),
            new("Misc/Guid.h") // EGuidFormats::DigitsWithHyphens для FGuid в path params
        };

        var implementations = client.Operations
            .Select(op => BuildPathMethodImplementation(client.Name, op))
            .Cast<AstNode>()
            .ToList();

        return new CppSourceFileNode(
            fileName: sourceFileName,
            headerComment: _options.Copyright != null ? new CommentNode(_options.Copyright, CommentKind.MultiLine) : null,
            includes: includes,
            anonymousNamespaceMembers: ImmutableList<AstNode>.Empty,
            implementations: implementations);
    }

    // ===== Include building =====

    private List<CppIncludeDirectiveNode> BuildEndpointHeaderIncludes(IrOperation operation, string headerFileName)
    {
        var includes = new List<CppIncludeDirectiveNode>
        {
            new("CoreMinimal.h"),
            new("Async/Future.h"),
            new(_options.BaseAsyncActionIncludePath),
            new($"../{_naming.ResultIncludePath(operation.MethodName)}"),
            new($"../{_naming.ParamsIncludePath(operation.MethodName)}"),
            new(headerFileName.Replace(".h", _options.GeneratedHeaderSuffix))
        };

        return includes;
    }

    private List<CppForwardDeclarationNode> BuildForwardDeclarations()
    {
        return new List<CppForwardDeclarationNode>
        {
            new(CppForwardDeclarationKind.Class, new IdentifierNode("FValIOClient")),
            new(CppForwardDeclarationKind.Struct, new IdentifierNode("FValIOResponse"))
        };
    }

    private List<CppUsingAliasNode> BuildUsingAliases()
    {
        return new List<CppUsingAliasNode>
        {
            new(
                new IdentifierNode("FValIOClientRef"),
                new GenericTypeNode(
                    new IdentifierNode("TSharedRef"),
                    new TypeNode[] { new SimpleTypeNode(new IdentifierNode("FValIOClient")) }))
        };
    }

    /// <summary>
    /// Строит DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam для операции
    /// </summary>
    private UDelegateDeclarationNode BuildDelegateDeclaration(IrOperation operation)
    {
        var delegateName = _naming.DelegateName(operation.MethodName);
        var resultTypeName = _naming.ResultStructName(operation.MethodName);

        return new UDelegateDeclarationNode(
            delegateName: new IdentifierNode(delegateName),
            parameters: new[]
            {
                new UDelegateParameter(
                    new SimpleTypeNode(new IdentifierNode(resultTypeName)),
                    new IdentifierNode("Result"))
            });
    }

    /// <summary>
    /// Строит объявление нативной TFuture функции для .h
    /// </summary>
    private CppFreeFunctionNode BuildNativeFunctionDeclaration(string clientName, IrOperation operation)
    {
        var funcName = _naming.NativeFunctionName(clientName, operation.MethodName);
        var resultTypeName = _naming.ResultStructName(operation.MethodName);
        var paramsTypeName = _naming.ParamsStructName(operation.MethodName);

        var returnType = new GenericTypeNode(
            new IdentifierNode("TFuture"),
            new TypeNode[] { new SimpleTypeNode(new IdentifierNode(resultTypeName)) });

        var parameters = new List<CppParameterNode>
        {
            new(new IdentifierNode("Client"),
                new SimpleTypeNode(new IdentifierNode("FValIOClientRef"))),
            new(new IdentifierNode("Params"),
                new SimpleTypeNode(new IdentifierNode(paramsTypeName)),
                isConst: true, isReference: true)
        };

        return new CppFreeFunctionNode(
            name: new IdentifierNode(funcName),
            returnType: returnType,
            parameters: parameters,
            isDeclarationOnly: true,
            documentation: BuildOperationDocumentation(operation));
    }

    /// <summary>
    /// Строит Blueprint Async Action класс
    /// </summary>
    private UClassDeclarationNode BuildAsyncActionClass(string clientName, IrOperation operation)
    {
        var className = _naming.AsyncActionClassName(clientName, operation.MethodName);
        var delegateName = _naming.DelegateName(operation.MethodName);
        var resultTypeName = _naming.ResultStructName(operation.MethodName);
        var paramsTypeName = _naming.ParamsStructName(operation.MethodName);
        var category = $"{_options.BlueprintCategory}|{UENamingConvention.ToPascalCase(clientName)}";

        var publicMembers = new List<AstNode>();
        var protectedMembers = new List<AstNode>();

        // UPROPERTY(BlueprintAssignable) delegate OnCompleted
        publicMembers.Add(new UPropertyFieldNode(
            name: new IdentifierNode("OnCompleted"),
            type: new SimpleTypeNode(new IdentifierNode(delegateName)),
            specifiers: UPropertySpecifiers.AssignableDelegate("On Completed")));

        // UPROPERTY(BlueprintReadOnly) LastResult
        publicMembers.Add(new UPropertyFieldNode(
            name: new IdentifierNode("LastResult"),
            type: new SimpleTypeNode(new IdentifierNode(resultTypeName)),
            specifiers: UPropertySpecifiers.ResultField(_options.BlueprintCategory)));

        // UFUNCTION(BlueprintCallable) static factory method
        var factoryParams = new List<CppParameterNode>
        {
            new(new IdentifierNode("WorldContextObject"),
                new SimpleTypeNode(new IdentifierNode("UObject")),
                isPointer: true),
            new(new IdentifierNode("ClientName"),
                new SimpleTypeNode(new IdentifierNode("FString")),
                isConst: true, isReference: true),
            new(new IdentifierNode("Params"),
                new SimpleTypeNode(new IdentifierNode(paramsTypeName)),
                isConst: true, isReference: true)
        };

        var displayName = FormatDisplayName(operation.MethodName);
        publicMembers.Add(new UFunctionDeclarationNode(
            name: new IdentifierNode(operation.MethodName),
            returnType: new SimpleTypeNode(new IdentifierNode($"{className}*")),
            parameters: factoryParams,
            specifiers: UFunctionSpecifiers.AsyncFactory(category, displayName),
            isStatic: true));

        // virtual void Activate() override;
        publicMembers.Add(new UFunctionDeclarationNode(
            name: new IdentifierNode("Activate"),
            returnType: new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true),
            isVirtual: true, isOverride: true));

        // void Complete(const Result& Result);
        publicMembers.Add(new UFunctionDeclarationNode(
            name: new IdentifierNode("Complete"),
            returnType: new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true),
            parameters: new[]
            {
                new CppParameterNode(
                    new IdentifierNode("Result"),
                    new SimpleTypeNode(new IdentifierNode(resultTypeName)),
                    isConst: true, isReference: true)
            }));

        // protected: GetWorldContext, GetClientName
        protectedMembers.Add(new UFunctionDeclarationNode(
            name: new IdentifierNode("GetWorldContext"),
            returnType: new SimpleTypeNode(new IdentifierNode("UObject*")),
            isVirtual: true, isOverride: true, isConst: true));

        protectedMembers.Add(new UFunctionDeclarationNode(
            name: new IdentifierNode("GetClientName"),
            returnType: new SimpleTypeNode(new IdentifierNode("FString")),
            isVirtual: true, isOverride: true, isConst: true));

        // public: State field
        var stateType = new GenericTypeNode(
            new IdentifierNode("FValIOAsyncState"),
            new TypeNode[] { new SimpleTypeNode(new IdentifierNode(paramsTypeName)) });

        publicMembers.Add(new UPropertyFieldNode(
            name: new IdentifierNode("State"),
            type: stateType,
            documentation: new CommentNode("Used by UValIOAsyncActionBase::NewAsyncObject.", CommentKind.MultiLine)));

        return new UClassDeclarationNode(
            name: new IdentifierNode(className),
            specifiers: UClassSpecifiers.AsyncAction(),
            apiMacro: _options.ModuleApiMacro,
            baseTypes: new TypeNode[] { new SimpleTypeNode(new IdentifierNode(_options.BaseAsyncActionClassName)) },
            publicMembers: publicMembers,
            protectedMembers: protectedMembers,
            documentation: new CommentNode($"Async action for {operation.MethodName}.", CommentKind.MultiLine));
    }

    /// <summary>
    /// Строит PathsLibrary класс (UBlueprintFunctionLibrary)
    /// </summary>
    private UClassDeclarationNode BuildPathsLibraryClass(IrClient client)
    {
        var className = _naming.PathsLibraryClassName(client.Name);
        var category = $"{_options.BlueprintCategory}|{UENamingConvention.ToPascalCase(client.Name)}|Paths";

        var publicMembers = new List<AstNode>();

        foreach (var op in client.Operations)
        {
            var pathMethodName = _naming.PathMethodName(op.MethodName);
            var displayName = FormatDisplayName(pathMethodName);

            var parameters = op.PathParameters
                .Select(p => new CppParameterNode(
                    new IdentifierNode(_naming.PropertyName(p.Name)),
                    _typeMapper.MapType(p.Type),
                    isConst: IsReferenceType(p.Type),
                    isReference: IsReferenceType(p.Type)))
                .ToList();

            publicMembers.Add(new UFunctionDeclarationNode(
                name: new IdentifierNode(pathMethodName),
                returnType: new SimpleTypeNode(new IdentifierNode("FString")),
                parameters: parameters,
                specifiers: UFunctionSpecifiers.PathBuilder(category, displayName),
                isStatic: true,
                documentation: BuildPathDocumentation(op)));
        }

        return new UClassDeclarationNode(
            name: new IdentifierNode(className),
            apiMacro: _options.ModuleApiMacro,
            baseTypes: new TypeNode[] { new SimpleTypeNode(new IdentifierNode("UBlueprintFunctionLibrary")) },
            publicMembers: publicMembers,
            documentation: new CommentNode(
                $"Blueprint Function Library for {client.Name} API path construction.",
                CommentKind.MultiLine));
    }

    // ===== Source (.cpp) building methods =====

    /// <summary>
    /// Строит Parse-функцию в анонимном namespace
    /// </summary>
    private CppParseFunctionNode BuildParseFunction(IrOperation operation)
    {
        var parseName = _naming.ParseFunctionName(operation.MethodName);
        var resultTypeName = _naming.ResultStructName(operation.MethodName);
        var hasSuccessBody = _resultBuilder.HasSuccessBody(operation);
        var successBodyTypeName = GetSuccessBodyTypeName(operation);

        var parameters = new List<CppParameterNode>
        {
            new(new IdentifierNode("R"),
                new SimpleTypeNode(new IdentifierNode("FValIOResponse")),
                isConst: true, isReference: true)
        };

        var statements = new List<StatementNode>();

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("Out"),
            new SimpleTypeNode(new IdentifierNode(resultTypeName))));

        statements.Add(new ExpressionStatementNode(
            new BinaryExpressionNode(
                new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("Out")), new IdentifierNode("StatusCode")),
                BinaryOperator.Assign,
                new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("R")), new IdentifierNode("StatusCode")),
                isAssignment: true)));

        var assignErrBody = new ExpressionStatementNode(
            new BinaryExpressionNode(
                new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("Out")), new IdentifierNode("ErrorBody")),
                BinaryOperator.Assign,
                new CallExpressionNode(
                    new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("Err")), new IdentifierNode("GetValue")),
                    Array.Empty<ExpressionNode>().ToImmutableList()),
                isAssignment: true));

        var assignFallback = new ExpressionStatementNode(
            new BinaryExpressionNode(
                new MemberAccessExpressionNode(
                    new MemberAccessExpressionNode(
                        new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("Out")), new IdentifierNode("ErrorBody")),
                        new IdentifierNode("Exception")),
                    new IdentifierNode("message")),
                BinaryOperator.Assign,
                new CallExpressionNode(
                    new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("R")), new IdentifierNode("GetBodyAsString")),
                    Array.Empty<ExpressionNode>().ToImmutableList()),
                isAssignment: true));

        var errIfStatement = new IfStatementNode(
            new CallExpressionNode(
                new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("Err")), new IdentifierNode("IsSet")),
                Array.Empty<ExpressionNode>().ToImmutableList()),
            new BlockNode(new StatementNode[] { assignErrBody }.ToImmutableList()),
            new BlockNode(new StatementNode[] { assignFallback }.ToImmutableList()));

        var errorBlockStatements = new List<StatementNode>
        {
            new VariableDeclarationStatementNode(
                new IdentifierNode("Err"),
                new SimpleTypeNode(new IdentifierNode("const TOptional<FRestApiResponse>")),
                new CallExpressionNode(
                    new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("R")), new IdentifierNode("GetBodyAs")),
                    Array.Empty<ExpressionNode>().ToImmutableList(),
                    genericTypeName: "FRestApiResponse")),
            errIfStatement
        };
        var errorThenBlock = new BlockNode(errorBlockStatements.ToImmutableList());

        if (hasSuccessBody)
        {
            var successThenStatements = new List<StatementNode>
            {
                new VariableDeclarationStatementNode(
                    new IdentifierNode("Dto"),
                    new SimpleTypeNode(new IdentifierNode($"const TOptional<{successBodyTypeName}>")),
                    new CallExpressionNode(
                        new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("R")), new IdentifierNode("GetBodyAs")),
                        Array.Empty<ExpressionNode>().ToImmutableList(),
                        genericTypeName: successBodyTypeName)),
                new IfStatementNode(
                    new BinaryExpressionNode(
                        new CallExpressionNode(
                            new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("Dto")), new IdentifierNode("IsSet")),
                            Array.Empty<ExpressionNode>().ToImmutableList()),
                        BinaryOperator.Equal,
                        new LiteralExpressionNode(true, LiteralKind.Boolean),
                        isAssignment: false),
                    new BlockNode(new StatementNode[]
                    {
                        new ExpressionStatementNode(
                            new BinaryExpressionNode(
                                new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("Out")), new IdentifierNode("SuccessBody")),
                                BinaryOperator.Assign,
                                new CallExpressionNode(
                                    new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("Dto")), new IdentifierNode("GetValue")),
                                    Array.Empty<ExpressionNode>().ToImmutableList()),
                                isAssignment: true))
                    }.ToImmutableList()))
            };
            var successCond = new CallExpressionNode(
                new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("R")), new IdentifierNode("IsSuccess")),
                Array.Empty<ExpressionNode>().ToImmutableList());
            var errorCond = new BinaryExpressionNode(
                new BinaryExpressionNode(
                    new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("R")), new IdentifierNode("StatusCode")),
                    BinaryOperator.GreaterThanOrEqual,
                    new LiteralExpressionNode(400, LiteralKind.Number)),
                BinaryOperator.LogicalAnd,
                new BinaryExpressionNode(
                    new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("R")), new IdentifierNode("StatusCode")),
                    BinaryOperator.LessThan,
                    new LiteralExpressionNode(600, LiteralKind.Number)));
            statements.Add(new IfStatementNode(
                successCond,
                new BlockNode(successThenStatements.ToImmutableList()),
                new BlockNode(new StatementNode[] { new IfStatementNode(errorCond, errorThenBlock) }.ToImmutableList())));
        }
        else
        {
            var notSuccessCond = new BinaryExpressionNode(
                new BinaryExpressionNode(
                    new CallExpressionNode(
                        new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("R")), new IdentifierNode("IsSuccess")),
                        Array.Empty<ExpressionNode>().ToImmutableList()),
                    BinaryOperator.Equal,
                    new LiteralExpressionNode(false, LiteralKind.Boolean)),
                BinaryOperator.LogicalAnd,
                new BinaryExpressionNode(
                    new BinaryExpressionNode(
                        new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("R")), new IdentifierNode("StatusCode")),
                        BinaryOperator.GreaterThanOrEqual,
                        new LiteralExpressionNode(400, LiteralKind.Number)),
                    BinaryOperator.LogicalAnd,
                    new BinaryExpressionNode(
                        new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("R")), new IdentifierNode("StatusCode")),
                        BinaryOperator.LessThan,
                        new LiteralExpressionNode(600, LiteralKind.Number))));
            statements.Add(new IfStatementNode(notSuccessCond, errorThenBlock));
        }

        statements.Add(new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("Out"))));

        var body = new BlockNode(statements.ToImmutableList());
        return new CppParseFunctionNode(
            name: new IdentifierNode(parseName),
            returnType: new SimpleTypeNode(new IdentifierNode(resultTypeName)),
            parameters: parameters,
            body: body);
    }

    /// <summary>
    /// Строит реализацию нативной функции
    /// </summary>
    private CppNativeApiFunctionNode BuildNativeFunctionImplementation(string clientName, IrOperation operation)
    {
        var funcName = _naming.NativeFunctionName(clientName, operation.MethodName);
        var resultTypeName = _naming.ResultStructName(operation.MethodName);
        var paramsTypeName = _naming.ParamsStructName(operation.MethodName);
        var parseFuncName = _naming.ParseFunctionName(operation.MethodName);
        var pathsClassName = _naming.PathsLibraryClassName(clientName);
        var pathMethodName = _naming.PathMethodName(operation.MethodName);
        var hasMultipleParamGroups = HasMultipleParamGroups(operation);
        var pathPrefix = hasMultipleParamGroups ? "Params.Route." : "Params.";
        var queryPrefix = hasMultipleParamGroups ? "Params.Query." : "Params.";
        var httpVerb = operation.HttpMethod.ToUpperInvariant() switch
        {
            "GET" => "Get", "POST" => "Post", "PUT" => "Put", "DELETE" => "Delete",
            "PATCH" => "Patch", "HEAD" => "Head", "OPTIONS" => "Options",
            _ => "Get"
        };

        var returnType = new GenericTypeNode(
            new IdentifierNode("TFuture"),
            new TypeNode[] { new SimpleTypeNode(new IdentifierNode(resultTypeName)) });

        var parameters = new List<CppParameterNode>
        {
            new(new IdentifierNode("Client"),
                new SimpleTypeNode(new IdentifierNode("FValIOClientRef"))),
            new(new IdentifierNode("Params"),
                new SimpleTypeNode(new IdentifierNode(paramsTypeName)),
                isConst: true, isReference: true)
        };

        var pathArgExprs = operation.PathParameters
            .Select(p => (ExpressionNode)new IdentifierExpressionNode(new IdentifierNode(pathPrefix + _naming.PropertyName(p.Name))))
            .ToImmutableList();
        var pathCall = new CallExpressionNode(
            new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode(pathsClassName)),
                new IdentifierNode(pathMethodName),
                useScopeOperator: true),
            pathArgExprs);

        var statements = new List<StatementNode>();
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("Path"),
            new SimpleTypeNode(new IdentifierNode("const FString")),
            pathCall));

        ExpressionNode requestChain = new CallExpressionNode(
            new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("Client")),
                new IdentifierNode("NewRequest"),
                useArrowOperator: true),
            new ExpressionNode[]
            {
                new MemberAccessExpressionNode(
                    new IdentifierExpressionNode(new IdentifierNode("EValIOHttpVerb")),
                    new IdentifierNode(httpVerb)),
                new IdentifierExpressionNode(new IdentifierNode("Path"))
            }.ToImmutableList());

        foreach (var qp in operation.QueryParameters)
        {
            var propName = queryPrefix + _naming.PropertyName(qp.Name);
            requestChain = new CallExpressionNode(
                new MemberAccessExpressionNode(requestChain, new IdentifierNode("AddQueryParam")),
                new ExpressionNode[]
                {
                    new LiteralExpressionNode(qp.Name, LiteralKind.String),
                    new CallExpressionNode(
                        new MemberAccessExpressionNode(
                            new IdentifierExpressionNode(new IdentifierNode("FString")),
                            new IdentifierNode("FromInt")),
                        new ExpressionNode[] { new IdentifierExpressionNode(new IdentifierNode(propName)) }.ToImmutableList())
                }.ToImmutableList());
        }

        requestChain = new CallExpressionNode(
            new MemberAccessExpressionNode(requestChain, new IdentifierNode("SetRequestName")),
            new ExpressionNode[] { new LiteralExpressionNode(operation.MethodName, LiteralKind.String) }.ToImmutableList());

        requestChain = new CallExpressionNode(
            new MemberAccessExpressionNode(requestChain, new IdentifierNode("Build")),
            ImmutableList<ExpressionNode>.Empty);

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("Request"),
            new SimpleTypeNode(new IdentifierNode("FValIORequest")),
            requestChain));

        var sendCall = new CallExpressionNode(
            new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("Client")),
                new IdentifierNode("Send"),
                useArrowOperator: true),
            new ExpressionNode[]
            {
                new CallExpressionNode(
                    new IdentifierExpressionNode(new IdentifierNode("MoveTemp")),
                    new ExpressionNode[] { new IdentifierExpressionNode(new IdentifierNode("Request")) }.ToImmutableList())
            }.ToImmutableList());

        var nextCall = new CallExpressionNode(
            new MemberAccessExpressionNode(sendCall, new IdentifierNode("Next")),
            new ExpressionNode[] { new IdentifierExpressionNode(new IdentifierNode(parseFuncName)) }.ToImmutableList());

        statements.Add(new ReturnStatementNode(nextCall));

        var body = new BlockNode(statements.ToImmutableList());
        return new CppNativeApiFunctionNode(
            name: new IdentifierNode(funcName),
            returnType: returnType,
            parameters: parameters,
            body: body);
    }

    /// <summary>
    /// Строит реализации методов Async Action класса
    /// </summary>
    private List<AstNode> BuildAsyncActionImplementations(string clientName, IrOperation operation)
    {
        var className = _naming.AsyncActionClassName(clientName, operation.MethodName);
        var resultTypeName = _naming.ResultStructName(operation.MethodName);
        var paramsTypeName = _naming.ParamsStructName(operation.MethodName);
        var nativeFuncName = _naming.NativeFunctionName(clientName, operation.MethodName);

        var implementations = new List<AstNode>();

        var factoryReturnExpr = new CallExpressionNode(
            new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("UValIOAsyncActionBase")),
                new IdentifierNode("NewAsyncObject"),
                useScopeOperator: true),
            new ExpressionNode[]
            {
                new IdentifierExpressionNode(new IdentifierNode("WorldContextObject")),
                new IdentifierExpressionNode(new IdentifierNode("ClientNameArg")),
                new IdentifierExpressionNode(new IdentifierNode("Params"))
            }.ToImmutableList(),
            genericTypeName: className);
        implementations.Add(new CppAsyncFactoryImplementationNode(
            className: new IdentifierNode(className),
            methodName: new IdentifierNode(operation.MethodName),
            returnType: new SimpleTypeNode(new IdentifierNode($"{className}*")),
            parameters: new[]
            {
                new CppParameterNode(new IdentifierNode("WorldContextObject"), new SimpleTypeNode(new IdentifierNode("UObject")), isPointer: true),
                new CppParameterNode(new IdentifierNode("ClientNameArg"), new SimpleTypeNode(new IdentifierNode("FString")), isConst: true, isReference: true),
                new CppParameterNode(new IdentifierNode("Params"), new SimpleTypeNode(new IdentifierNode(paramsTypeName)), isConst: true, isReference: true)
            },
            body: new BlockNode(new StatementNode[]
            {
                new ReturnStatementNode(factoryReturnExpr)
            }.ToImmutableList())));

        var completeStatements = new List<StatementNode>
        {
            new ExpressionStatementNode(
                new BinaryExpressionNode(
                    new IdentifierExpressionNode(new IdentifierNode("LastResult")),
                    BinaryOperator.Assign,
                    new IdentifierExpressionNode(new IdentifierNode("Result")),
                    isAssignment: true)),
            new ExpressionStatementNode(
                new CallExpressionNode(
                    new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("OnCompleted")), new IdentifierNode("Broadcast")),
                    new ExpressionNode[] { new IdentifierExpressionNode(new IdentifierNode("Result")) }.ToImmutableList())),
            new ExpressionStatementNode(
                new CallExpressionNode(
                    new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("this")), new IdentifierNode("SetReadyToDestroy"), useArrowOperator: true),
                    ImmutableList<ExpressionNode>.Empty))
        };
        implementations.Add(new CppAsyncCompleteImplementationNode(
            className: new IdentifierNode(className),
            methodName: new IdentifierNode("Complete"),
            returnType: new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true),
            parameters: new[]
            {
                new CppParameterNode(new IdentifierNode("Result"), new SimpleTypeNode(new IdentifierNode(resultTypeName)), isConst: true, isReference: true)
            },
            body: new BlockNode(completeStatements.ToImmutableList())));

        var lambdaBody = new BlockNode(new StatementNode[]
        {
            new ReturnStatementNode(new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode(nativeFuncName)),
                new ExpressionNode[]
                {
                    new IdentifierExpressionNode(new IdentifierNode("Client")),
                    new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("State")), new IdentifierNode("Params"))
                }.ToImmutableList()))
        }.ToImmutableList());
        var activateLambda = new CppLambdaExpressionNode(
            new[] { "this" },
            new[] { new CppParameterNode(new IdentifierNode("Client"), new SimpleTypeNode(new IdentifierNode("FValIOClientRef"))) },
            lambdaBody);
        var runWithCall = new CallExpressionNode(
            new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("this")),
                new IdentifierNode("RunWithClientAndComplete"),
                useArrowOperator: true),
            new ExpressionNode[] { activateLambda }.ToImmutableList(),
            genericTypeName: $"{className}, {resultTypeName}");
        implementations.Add(new CppAsyncActivateImplementationNode(
            className: new IdentifierNode(className),
            body: new BlockNode(new StatementNode[]
            {
                new ExpressionStatementNode(runWithCall)
            }.ToImmutableList())));

        return implementations;
    }

    /// <summary>
    /// Строит реализацию метода PathsLibrary
    /// </summary>
    private CppPathBuilderImplementationNode BuildPathMethodImplementation(string clientName, IrOperation operation)
    {
        var pathsClassName = _naming.PathsLibraryClassName(clientName);
        var pathMethodName = _naming.PathMethodName(operation.MethodName);

        var parameters = operation.PathParameters
            .Select(p => new CppParameterNode(
                new IdentifierNode(_naming.PropertyName(p.Name)),
                _typeMapper.MapType(p.Type),
                isConst: IsReferenceType(p.Type),
                isReference: IsReferenceType(p.Type)))
            .ToList();

        var pathParamSpecs = operation.PathParameters.Select(p =>
        {
            var propName = _naming.PropertyName(p.Name);
            var isGuid = _typeMapper.IsGuidType(p.Type);
            var intermediateVarName = isGuid ? propName + "Str" : null;
            var printfArgExpr = _typeMapper.GetPrintfArgumentExpression(p.Type, propName, intermediateVarName);

            return (OriginalName: p.Name, PropertyName: propName, PrintfSpecifier: _typeMapper.GetPrintfSpecifier(p.Type),
                PrintfArgExpression: printfArgExpr, IsGuid: isGuid, IntermediateVarName: intermediateVarName);
        }).ToList();

        var statements = new List<StatementNode>();

        foreach (var param in pathParamSpecs)
        {
            if (param.IsGuid && param.IntermediateVarName != null)
            {
                var toStrCall = new CallExpressionNode(
                    new MemberAccessExpressionNode(
                        new IdentifierExpressionNode(new IdentifierNode(param.PropertyName)),
                        new IdentifierNode("ToString")),
                    new ExpressionNode[] { new MemberAccessExpressionNode(
                        new IdentifierExpressionNode(new IdentifierNode("EGuidFormats")),
                        new IdentifierNode("DigitsWithHyphens"),
                        useScopeOperator: true) }.ToImmutableList());
                statements.Add(new VariableDeclarationStatementNode(
                    new IdentifierNode(param.IntermediateVarName),
                    new SimpleTypeNode(new IdentifierNode("const FString")),
                    toStrCall));
            }
        }

        var formatStr = pathParamSpecs.Aggregate(operation.Path, (acc, p) =>
            acc.Replace($"{{{p.OriginalName}}}", p.PrintfSpecifier));
        var printfArgs = new List<ExpressionNode>
        {
            new LiteralExpressionNode(formatStr, LiteralKind.String)
        };
        printfArgs.AddRange(pathParamSpecs.Select(p =>
        {
            var needsDeref = p.PrintfArgExpression.StartsWith("*");
            var ident = needsDeref ? p.PrintfArgExpression.TrimStart('*') : p.PrintfArgExpression;
            var expr = (ExpressionNode)new IdentifierExpressionNode(new IdentifierNode(ident));
            return needsDeref ? new UnaryExpressionNode(UnaryOperator.Dereference, expr) : expr;
        }));

        var printfCall = new CallExpressionNode(
            new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("FString")),
                new IdentifierNode("Printf"),
                useScopeOperator: true),
            printfArgs.ToImmutableList());
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("Url"),
            new SimpleTypeNode(new IdentifierNode("const FString")),
            printfCall));
        statements.Add(new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("Url"))));

        var body = new BlockNode(statements.ToImmutableList());
        return new CppPathBuilderImplementationNode(
            className: new IdentifierNode(pathsClassName),
            methodName: new IdentifierNode(pathMethodName),
            parameters: parameters,
            body: body);
    }

    // ===== Helper methods =====

    private string GetSuccessBodyTypeName(IrOperation operation)
    {
        var successResponse = operation.Responses.FirstOrDefault(r =>
            r.StatusCode.StartsWith('2') && r.Type != null);

        if (successResponse?.Type is IrModelReference modelRef)
            return _naming.StructName(modelRef.ModelName);

        if (successResponse?.Type != null)
        {
            var mapped = _typeMapper.MapType(successResponse.Type);
            return mapped.ToString() ?? "void";
        }

        return "void";
    }

    private bool HasMultipleParamGroups(IrOperation operation)
    {
        var count = 0;
        if (operation.PathParameters.Count > 0) count++;
        if (operation.QueryParameters.Count > 0) count++;
        if (operation.HeaderParameters.Count > 0) count++;
        if (operation.RequestBody != null) count++;
        return count > 1;
    }

    private static bool IsReferenceType(IrType type)
    {
        return type switch
        {
            IrPrimitiveType p => p.PrimitiveKind is IrPrimitiveKind.String or IrPrimitiveKind.Uuid,
            IrModelReference => true,
            IrArrayType => true,
            IrDictionaryType => true,
            _ => false
        };
    }

    private CommentNode? BuildOperationDocumentation(IrOperation operation)
    {
        var text = $"{operation.HttpMethod.ToUpperInvariant()} {operation.Path}";
        if (!string.IsNullOrWhiteSpace(operation.Summary))
            text = $"{operation.Summary}\n{text}";
        return new CommentNode(text, CommentKind.MultiLine);
    }

    private CommentNode? BuildPathDocumentation(IrOperation operation)
    {
        return new CommentNode(
            $"{operation.HttpMethod.ToUpperInvariant()} {operation.Path}",
            CommentKind.MultiLine);
    }

    private static string FormatDisplayName(string methodName)
    {
        // Convert PascalCase to "Pascal Case"
        return System.Text.RegularExpressions.Regex.Replace(
            methodName, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
    }
}
