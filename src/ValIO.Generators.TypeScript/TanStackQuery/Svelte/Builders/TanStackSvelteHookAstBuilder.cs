using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.JsDoc;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.TanStackQuery.Svelte.Builders;

/// <summary>
/// Builds AST for <c>@tanstack/svelte-query</c> hook files
/// (<c>createQuery(() =&gt; ({ ... }))</c> / <c>createMutation(() =&gt; ({ ... }))</c>).
/// Uses the accessor pattern even though Svelte technically allows a plain
/// object — strict-mode tsc + Svelte v5 runes prefer the getter form.
/// </summary>
public sealed class TanStackSvelteHookAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptParamsAstBuilder _paramsBuilder;
    private readonly TypeScriptResultsAstBuilder _resultsBuilder;

    public TanStackSvelteHookAstBuilder(
        INamingConverter namingConverter,
        Func<ValIO.Core.IR.Types.IrType, TypeNode> typeBuilder,
        string? resultSuffix = null,
        string? resultPrefix = null)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _paramsBuilder = new TypeScriptParamsAstBuilder(namingConverter, typeBuilder);
        _resultsBuilder = new TypeScriptResultsAstBuilder(namingConverter, typeBuilder, resultSuffix, resultPrefix);
    }

    public SourceFileNode BuildHookSourceFile(IrOperation operation, string operationFileName, string clientSuffix, bool isMutation)
    {
        var hookBaseName = _namingConverter.ToPascalCase(operation.MethodName);
        var hasParams = _paramsBuilder.HasParameters(operation);
        var paramsTypeName = hasParams ? _paramsBuilder.ParamsInterfaceName(operation.MethodName) : null;
        var resultTypeName = _resultsBuilder.HasResponsesWithBody(operation) ? _resultsBuilder.ResultTypeName(operation.MethodName) : null;

        var imports = BuildImports(operation, operationFileName, clientSuffix, hasParams, paramsTypeName, resultTypeName, isMutation);
        var declarations = isMutation
            ? ImmutableList<DeclarationNode>.Empty.Add(BuildUseMutationFn(operation, hookBaseName, paramsTypeName, resultTypeName, hasParams))
            : ImmutableList<DeclarationNode>.Empty
                .Add(BuildGetQueryKeyFn(operation, hookBaseName, paramsTypeName, hasParams))
                .Add(BuildUseQueryFn(operation, hookBaseName, paramsTypeName, resultTypeName, hasParams));

        return new SourceFileNode(imports: imports, declarations: declarations,
            fileDocumentation: TypeScriptDoc.FileHeader(
                $"{operationFileName}.query.ts",
                $"endpoints/{operationFileName}",
                $"TanStack Query (Svelte) hooks for the {operation.MethodName} operation."));
    }

    private static IReadOnlyList<ImportNode> BuildImports(
        IrOperation operation, string operationFileName, string clientSuffix,
        bool hasParams, string? paramsTypeName, string? resultTypeName, bool isMutation)
    {
        var imports = ImmutableList.CreateBuilder<ImportNode>();
        imports.Add(new ImportNode(
            items: ImmutableList.Create(new IdentifierNode(isMutation ? "createMutation" : "createQuery")),
            from: "@tanstack/svelte-query",
            kind: ImportKind.Named));

        imports.Add(new ImportNode(
            items: ImmutableList.Create(new IdentifierNode(operation.MethodName)),
            from: $"./{operationFileName}.{clientSuffix}",
            kind: ImportKind.Named));

        if (hasParams && paramsTypeName != null)
            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(paramsTypeName)),
                from: $"./{operationFileName}.params",
                kind: ImportKind.Named,
                isTypeOnly: true));

        if (resultTypeName != null)
            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(resultTypeName)),
                from: $"./{operationFileName}.results",
                kind: ImportKind.Named,
                isTypeOnly: true));

        return imports.ToImmutable();
    }

    private FunctionDeclarationNode BuildGetQueryKeyFn(IrOperation operation, string hookBaseName, string? paramsTypeName, bool hasParams)
    {
        var elements = new List<ExpressionNode>();
        foreach (var part in GetQueryKeyPathParts(operation))
            elements.Add(String(part));

        if (hasParams)
        {
            var paramsRef = Id("params");
            var cond = new ConditionalExpressionNode(
                paramsRef,
                new ArrayLiteralExpressionNode(ImmutableList.Create<ExpressionNode>(paramsRef)),
                new ArrayLiteralExpressionNode(ImmutableList<ExpressionNode>.Empty));
            elements.Add(new SpreadExpressionNode(cond));
        }

        var parameters = hasParams && paramsTypeName != null
            ? ImmutableList.Create(new ParameterDeclarationNode(new IdentifierNode("params"), Simple(paramsTypeName), isRequired: false))
            : ImmutableList<ParameterDeclarationNode>.Empty;

        return new FunctionDeclarationNode(
            name: new IdentifierNode($"get{hookBaseName}QueryKey"),
            returnType: new ArrayTypeNode(Simple("unknown", isPrimitive: true), isReadOnly: true),
            parameters: parameters,
            body: new BlockNode(ImmutableList.Create<StatementNode>(
                new ReturnStatementNode(new ArrayLiteralExpressionNode(elements.ToImmutableList(), preferMultilineFormat: true)))),
            isArrowFunction: true,
            isExported: true);
    }

    private FunctionDeclarationNode BuildUseQueryFn(IrOperation operation, string hookBaseName, string? paramsTypeName, string? resultTypeName, bool hasParams)
    {
        var queryOptions = new List<PropertyAssignmentNode>
        {
            new(new IdentifierNode("queryKey"), new CallExpressionNode(
                Id($"get{hookBaseName}QueryKey"),
                hasParams ? ImmutableList.Create<ExpressionNode>(Id("params")) : ImmutableList<ExpressionNode>.Empty)),
            new(new IdentifierNode("queryFn"), new ArrowFunctionExpressionNode(
                ImmutableList<ParameterDeclarationNode>.Empty,
                new CallExpressionNode(Id(operation.MethodName),
                    hasParams ? ImmutableList.Create<ExpressionNode>(Id("params")) : ImmutableList<ExpressionNode>.Empty)))
        };
        queryOptions.Add(new PropertyAssignmentNode(
            new IdentifierNode("options"),
            new BinaryExpressionNode(Id("options"), BinaryOperator.NullishCoalescing, new ObjectLiteralExpressionNode(ImmutableList<PropertyAssignmentNode>.Empty)),
            isSpread: true));

        var arrow = new ArrowFunctionExpressionNode(
            ImmutableList<ParameterDeclarationNode>.Empty,
            new ObjectLiteralExpressionNode(queryOptions.ToImmutableList(), preferMultilineFormat: true));
        var call = new CallExpressionNode(Id("createQuery"), ImmutableList.Create<ExpressionNode>(arrow), genericTypeName: resultTypeName);

        var parameters = new List<ParameterDeclarationNode>();
        if (hasParams && paramsTypeName != null)
            parameters.Add(new ParameterDeclarationNode(new IdentifierNode("params"), Simple(paramsTypeName), isRequired: true));
        parameters.Add(new ParameterDeclarationNode(
            new IdentifierNode("options"),
            new SimpleTypeNode(new IdentifierNode("object"), isPrimitive: true),
            isRequired: false));

        return new FunctionDeclarationNode(
            name: new IdentifierNode($"use{hookBaseName}"),
            returnType: null,
            parameters: parameters.ToImmutableList(),
            body: new BlockNode(ImmutableList.Create<StatementNode>(new ReturnStatementNode(call))),
            isArrowFunction: true,
            isExported: true);
    }

    private FunctionDeclarationNode BuildUseMutationFn(IrOperation operation, string hookBaseName, string? paramsTypeName, string? resultTypeName, bool hasParams)
    {
        var variablesType = hasParams && paramsTypeName != null ? Simple(paramsTypeName) : Simple("void", isPrimitive: true);
        var mutationFnParameters = hasParams
            ? ImmutableList.Create(new ParameterDeclarationNode(new IdentifierNode("variables"), variablesType, isRequired: true))
            : ImmutableList<ParameterDeclarationNode>.Empty;
        var clientArgs = hasParams
            ? ImmutableList.Create<ExpressionNode>(Id("variables"))
            : ImmutableList<ExpressionNode>.Empty;

        var mutationOptions = new List<PropertyAssignmentNode>
        {
            new(new IdentifierNode("mutationFn"), new ArrowFunctionExpressionNode(
                mutationFnParameters,
                new CallExpressionNode(Id(operation.MethodName), clientArgs))),
            new(
                new IdentifierNode("options"),
                new BinaryExpressionNode(Id("options"), BinaryOperator.NullishCoalescing, new ObjectLiteralExpressionNode(ImmutableList<PropertyAssignmentNode>.Empty)),
                isSpread: true)
        };

        var arrow = new ArrowFunctionExpressionNode(
            ImmutableList<ParameterDeclarationNode>.Empty,
            new ObjectLiteralExpressionNode(mutationOptions.ToImmutableList(), preferMultilineFormat: true));
        var generic = resultTypeName != null
            ? $"{resultTypeName}, Error, {RenderTypeName(variablesType)}"
            : null;
        var call = new CallExpressionNode(Id("createMutation"), ImmutableList.Create<ExpressionNode>(arrow), genericTypeName: generic);

        return new FunctionDeclarationNode(
            name: new IdentifierNode($"use{hookBaseName}"),
            returnType: null,
            parameters: ImmutableList.Create(new ParameterDeclarationNode(
                new IdentifierNode("options"),
                new SimpleTypeNode(new IdentifierNode("object"), isPrimitive: true),
                isRequired: false)),
            body: new BlockNode(ImmutableList.Create<StatementNode>(new ReturnStatementNode(call))),
            isArrowFunction: true,
            isExported: true);
    }

    private static IReadOnlyList<string> GetQueryKeyPathParts(IrOperation operation)
    {
        var path = operation.Path.Trim('/');
        return string.IsNullOrEmpty(path)
            ? ["api"]
            : path.Split('/').Where(part => !string.IsNullOrEmpty(part)).ToList();
    }

    private static SimpleTypeNode Simple(string name, bool isPrimitive = false) =>
        new(new IdentifierNode(name), isPrimitive);

    private static IdentifierExpressionNode Id(string name) =>
        new(new IdentifierNode(name));

    private static LiteralExpressionNode String(string value) =>
        new(value, LiteralKind.String);

    private static string RenderTypeName(TypeNode type) =>
        type is SimpleTypeNode simple ? simple.Name.Name : type.ToString() ?? "unknown";
}
