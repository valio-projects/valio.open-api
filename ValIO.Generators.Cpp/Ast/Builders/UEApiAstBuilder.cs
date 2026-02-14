using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
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
    /// Строит пару (header, source) файлов для клиента
    /// </summary>
    public (CppHeaderFileNode Header, CppSourceFileNode Source) Build(IrClient client)
    {
        var header = BuildHeader(client);
        var source = BuildSource(client);
        return (header, source);
    }

    /// <summary>
    /// Строит заголовочный файл (.h) для клиента
    /// </summary>
    private CppHeaderFileNode BuildHeader(IrClient client)
    {
        var headerFileName = _naming.ApiHeaderFileName(client.Name);
        var declarations = new List<AstNode>();
        var includes = BuildHeaderIncludes(client, headerFileName);
        var forwardDecls = BuildForwardDeclarations();
        var usingAliases = BuildUsingAliases();

        // 1. Delegate declarations
        foreach (var op in client.Operations)
        {
            declarations.Add(BuildDelegateDeclaration(op));
        }

        // 2. Native function declarations
        foreach (var op in client.Operations)
        {
            declarations.Add(BuildNativeFunctionDeclaration(client.Name, op));
        }

        // 3. Async Action classes
        foreach (var op in client.Operations)
        {
            declarations.Add(BuildAsyncActionClass(client.Name, op));
        }

        // 4. PathsLibrary class
        declarations.Add(BuildPathsLibraryClass(client));

        return new CppHeaderFileNode(
            fileName: headerFileName,
            headerComment: _options.Copyright != null ? new CommentNode(_options.Copyright, CommentKind.MultiLine) : null,
            includes: includes,
            forwardDeclarations: forwardDecls,
            usingAliases: usingAliases,
            declarations: declarations);
    }

    /// <summary>
    /// Строит файл реализации (.cpp) для клиента
    /// </summary>
    private CppSourceFileNode BuildSource(IrClient client)
    {
        var sourceFileName = _naming.ApiSourceFileName(client.Name);
        var headerFileName = _naming.ApiHeaderFileName(client.Name);

        var includes = new List<CppIncludeDirectiveNode>
        {
            new(headerFileName),
            new(_options.ValIOClientIncludePath),
            new(_options.ValIORequestBuilderIncludePath),
            new(_options.ValIOMultipartFormDataIncludePath),
            new(_options.ValIOTypesIncludePath),
            new(_options.ValIOResponseIncludePath),
            new("Async/Async.h")
        };

        // Anonymous namespace: parse functions
        var anonymousMembers = new List<AstNode>();
        foreach (var op in client.Operations)
        {
            anonymousMembers.Add(BuildParseFunction(op));
        }

        // Implementations
        var implementations = new List<AstNode>();

        // Native function implementations
        foreach (var op in client.Operations)
        {
            implementations.Add(BuildNativeFunctionImplementation(client.Name, op));
        }

        // Async action method implementations
        foreach (var op in client.Operations)
        {
            implementations.AddRange(BuildAsyncActionImplementations(client.Name, op));
        }

        // Paths implementations
        foreach (var op in client.Operations)
        {
            implementations.Add(BuildPathMethodImplementation(client.Name, op));
        }

        return new CppSourceFileNode(
            fileName: sourceFileName,
            headerComment: _options.Copyright != null ? new CommentNode(_options.Copyright, CommentKind.MultiLine) : null,
            includes: includes,
            anonymousNamespaceMembers: anonymousMembers,
            implementations: implementations);
    }

    // ===== Header building methods =====

    private List<CppIncludeDirectiveNode> BuildHeaderIncludes(IrClient client, string headerFileName)
    {
        var includes = new List<CppIncludeDirectiveNode>
        {
            new("CoreMinimal.h"),
            new("Async/Future.h"),
            new(_options.BaseAsyncActionIncludePath),
            new("Kismet/BlueprintFunctionLibrary.h"),
            new(headerFileName.Replace(".h", _options.GeneratedHeaderSuffix))
        };

        // Add includes for result and params types
        foreach (var op in client.Operations)
        {
            includes.Add(new CppIncludeDirectiveNode(_naming.ResultFileName(op.MethodName)));
            includes.Add(new CppIncludeDirectiveNode(_naming.ParamsFileName(op.MethodName)));
        }

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
    private CppFreeFunctionNode BuildParseFunction(IrOperation operation)
    {
        var parseName = _naming.ParseFunctionName(operation.MethodName);
        var resultTypeName = _naming.ResultStructName(operation.MethodName);

        var parameters = new List<CppParameterNode>
        {
            new(new IdentifierNode("R"),
                new SimpleTypeNode(new IdentifierNode("FValIOResponse")),
                isConst: true, isReference: true)
        };

        // Body is represented as a block that the renderer will handle specially
        // via metadata on the function node
        return new CppFreeFunctionNode(
            name: new IdentifierNode(parseName),
            returnType: new SimpleTypeNode(new IdentifierNode(resultTypeName)),
            parameters: parameters,
            body: new BlockNode(),
            isDeclarationOnly: false,
            metadata: new Dictionary<string, object>
            {
                ["ParseFunction"] = true,
                ["ResultTypeName"] = resultTypeName,
                ["HasSuccessBody"] = _resultBuilder.HasSuccessBody(operation),
                ["SuccessBodyTypeName"] = GetSuccessBodyTypeName(operation)
            }.ToImmutableDictionary());
    }

    /// <summary>
    /// Строит реализацию нативной функции
    /// </summary>
    private CppFreeFunctionNode BuildNativeFunctionImplementation(string clientName, IrOperation operation)
    {
        var funcName = _naming.NativeFunctionName(clientName, operation.MethodName);
        var resultTypeName = _naming.ResultStructName(operation.MethodName);
        var paramsTypeName = _naming.ParamsStructName(operation.MethodName);
        var parseFuncName = _naming.ParseFunctionName(operation.MethodName);
        var pathsClassName = _naming.PathsLibraryClassName(clientName);
        var pathMethodName = _naming.PathMethodName(operation.MethodName);

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
            body: new BlockNode(),
            isDeclarationOnly: false,
            metadata: new Dictionary<string, object>
            {
                ["NativeApiFunction"] = true,
                ["HttpMethod"] = operation.HttpMethod.ToUpperInvariant(),
                ["PathsClassName"] = pathsClassName,
                ["PathMethodName"] = pathMethodName,
                ["ParseFunctionName"] = parseFuncName,
                ["RequestName"] = operation.MethodName,
                ["PathParams"] = operation.PathParameters.Select(p => _naming.PropertyName(p.Name)).ToList(),
                ["QueryParams"] = operation.QueryParameters.Select(p => new { Name = p.Name, PropertyName = _naming.PropertyName(p.Name) }).ToList(),
                ["HasBody"] = operation.RequestBody != null,
                ["ContentType"] = operation.RequestBody?.ContentType ?? "",
                ["HasMultipleParamGroups"] = HasMultipleParamGroups(operation)
            }.ToImmutableDictionary());
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

        // Static factory implementation
        implementations.Add(new CppMethodImplementationNode(
            className: new IdentifierNode(className),
            methodName: new IdentifierNode(operation.MethodName),
            returnType: new SimpleTypeNode(new IdentifierNode($"{className}*")),
            parameters: new[]
            {
                new CppParameterNode(new IdentifierNode("WorldContextObject"), new SimpleTypeNode(new IdentifierNode("UObject")), isPointer: true),
                new CppParameterNode(new IdentifierNode("ClientNameArg"), new SimpleTypeNode(new IdentifierNode("FString")), isConst: true, isReference: true),
                new CppParameterNode(new IdentifierNode("Params"), new SimpleTypeNode(new IdentifierNode(paramsTypeName)), isConst: true, isReference: true)
            },
            body: new BlockNode(),
            metadata: new Dictionary<string, object>
            {
                ["AsyncFactory"] = true,
                ["ClassName"] = className
            }.ToImmutableDictionary()));

        // Complete implementation
        implementations.Add(new CppMethodImplementationNode(
            className: new IdentifierNode(className),
            methodName: new IdentifierNode("Complete"),
            returnType: new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true),
            parameters: new[]
            {
                new CppParameterNode(new IdentifierNode("Result"), new SimpleTypeNode(new IdentifierNode(resultTypeName)), isConst: true, isReference: true)
            },
            body: new BlockNode(),
            metadata: new Dictionary<string, object>
            {
                ["AsyncComplete"] = true
            }.ToImmutableDictionary()));

        // Activate implementation
        implementations.Add(new CppMethodImplementationNode(
            className: new IdentifierNode(className),
            methodName: new IdentifierNode("Activate"),
            returnType: new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true),
            parameters: null,
            body: new BlockNode(),
            metadata: new Dictionary<string, object>
            {
                ["AsyncActivate"] = true,
                ["ClassName"] = className,
                ["ResultTypeName"] = resultTypeName,
                ["NativeFunctionName"] = nativeFuncName
            }.ToImmutableDictionary()));

        return implementations;
    }

    /// <summary>
    /// Строит реализацию метода PathsLibrary
    /// </summary>
    private CppMethodImplementationNode BuildPathMethodImplementation(string clientName, IrOperation operation)
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

        return new CppMethodImplementationNode(
            className: new IdentifierNode(pathsClassName),
            methodName: new IdentifierNode(pathMethodName),
            returnType: new SimpleTypeNode(new IdentifierNode("FString")),
            parameters: parameters,
            body: new BlockNode(),
            metadata: new Dictionary<string, object>
            {
                ["PathBuilder"] = true,
                ["UrlTemplate"] = operation.Path,
                ["PathParams"] = operation.PathParameters.Select(p => new
                {
                    OriginalName = p.Name,
                    PropertyName = _naming.PropertyName(p.Name),
                    PrintfSpecifier = _typeMapper.GetPrintfSpecifier(p.Type),
                    NeedsDereference = _typeMapper.NeedsDereference(p.Type)
                }).ToList<object>()
            }.ToImmutableDictionary());
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
