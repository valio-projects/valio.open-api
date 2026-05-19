using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;

/// <summary>
/// Базовый билдер AST для «лёгких» framework-обёрток над TanStack Query
/// (Angular/Solid/Svelte). Производит файл с тремя экспортами:
/// <list type="bullet">
/// <item><c>get&lt;Op&gt;QueryKey</c> — readonly unknown[] на основе имени операции и params.</item>
/// <item><c>use&lt;Op&gt;</c> — query или mutation хук, без аннотации возвращаемого типа.</item>
/// </list>
/// Конкретные фреймворки подменяют пакет (<c>@tanstack/...-query</c>),
/// имена функций (<c>injectQuery</c>/<c>createQuery</c>) и обёртку для объекта опций
/// (плоский объект, либо <c>() =&gt; ({ ... })</c>).
/// </summary>
public abstract class TanStackFrameworkHookAstBuilderBase
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptParamsAstBuilder _paramsBuilder;
    private readonly TypeScriptResultsAstBuilder _resultsBuilder;
    private readonly string _clientSuffix;

    protected TanStackFrameworkHookAstBuilderBase(
        INamingConverter namingConverter,
        Func<Core.IR.Types.IrType, TypeNode> typeBuilder,
        string clientSuffix)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _clientSuffix = clientSuffix ?? throw new ArgumentNullException(nameof(clientSuffix));
        _paramsBuilder = new TypeScriptParamsAstBuilder(namingConverter, typeBuilder);
        _resultsBuilder = new TypeScriptResultsAstBuilder(namingConverter, typeBuilder);
    }

    /// <summary>Имя npm-пакета, импортируемого для query/mutation API (например, <c>@tanstack/solid-query</c>).</summary>
    protected abstract string PackageName { get; }

    /// <summary>Имя функции для query (<c>injectQuery</c>/<c>createQuery</c>).</summary>
    protected abstract string QueryFnName { get; }

    /// <summary>Имя функции для mutation (<c>injectMutation</c>/<c>createMutation</c>).</summary>
    protected abstract string MutationFnName { get; }

    /// <summary>Оборачивать ли объект опций в getter <c>() =&gt; ({ ... })</c> (Angular/Solid) или передавать «как есть» (Svelte).</summary>
    protected abstract bool WrapOptionsInGetter { get; }

    /// <summary>
    /// Строит SourceFileNode для hook-файла операции в стиле выбранного фреймворка.
    /// </summary>
    public SourceFileNode BuildHookSourceFile(
        IrOperation operation,
        string operationFileName,
        bool isMutation)
    {
        var hookBaseName = _namingConverter.ToPascalCase(operation.MethodName);
        var hasParams = _paramsBuilder.HasParameters(operation);
        var paramsTypeName = hasParams ? _paramsBuilder.ParamsInterfaceName(operation.MethodName) : null;

        var resultTypeName = _resultsBuilder.HasResponsesWithBody(operation)
            ? _resultsBuilder.ResultTypeName(operation.MethodName)
            : null;

        var imports = BuildImports(operation, operationFileName, hasParams, paramsTypeName, resultTypeName, isMutation);
        var declarations = isMutation
            ? BuildMutationDeclarations(operation, hookBaseName, paramsTypeName, hasParams)
            : BuildQueryDeclarations(operation, hookBaseName, paramsTypeName, hasParams);

        return new SourceFileNode(imports: imports, declarations: declarations);
    }

    private IReadOnlyList<ImportNode> BuildImports(
        IrOperation operation,
        string operationFileName,
        bool hasParams,
        string? paramsTypeName,
        string? resultTypeName,
        bool isMutation)
    {
        var tanstackItem = isMutation ? MutationFnName : QueryFnName;

        var items = new List<ImportNode>
        {
            new(items: ImmutableList.Create(new IdentifierNode(tanstackItem)),
                from: PackageName,
                kind: ImportKind.Named),
            new(items: ImmutableList.Create(new IdentifierNode(operation.MethodName)),
                from: $"./{operationFileName}.{_clientSuffix}",
                kind: ImportKind.Named),
        };

        if (hasParams && paramsTypeName != null)
        {
            items.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(paramsTypeName)),
                from: $"./{operationFileName}.params",
                kind: ImportKind.Named,
                isTypeOnly: true));
        }

        if (resultTypeName != null)
        {
            items.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(resultTypeName)),
                from: $"./{operationFileName}.results",
                kind: ImportKind.Named,
                isTypeOnly: true));
        }

        return items;
    }

    private IReadOnlyList<DeclarationNode> BuildQueryDeclarations(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        var decls = new List<DeclarationNode>
        {
            BuildQueryKeyDeclaration(operation, hookBaseName, paramsTypeName, hasParams),
            BuildUseQueryDeclaration(operation, hookBaseName, paramsTypeName, hasParams),
        };
        return decls;
    }

    private IReadOnlyList<DeclarationNode> BuildMutationDeclarations(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        return new DeclarationNode[]
        {
            BuildUseMutationDeclaration(operation, hookBaseName, paramsTypeName, hasParams),
        };
    }

    /// <summary>
    /// <c>export const get&lt;Op&gt;QueryKey = (params?: &lt;Params&gt;): readonly unknown[] =&gt; [&quot;methodName&quot;, params];</c>
    /// </summary>
    private DeclarationNode BuildQueryKeyDeclaration(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        var keyElements = new List<ExpressionNode>
        {
            new LiteralExpressionNode(_namingConverter.ToCamelCase(operation.MethodName), LiteralKind.String),
        };
        if (hasParams)
            keyElements.Add(new IdentifierExpressionNode(new IdentifierNode("params")));

        var arrayLiteral = new ArrayLiteralExpressionNode(keyElements.ToImmutableList());

        var arrowParams = new List<ParameterDeclarationNode>();
        if (hasParams && paramsTypeName != null)
        {
            arrowParams.Add(new ParameterDeclarationNode(
                new IdentifierNode("params"),
                BuildSimpleType(paramsTypeName),
                isRequired: true));
        }

        var arrow = new ArrowFunctionExpressionNode(
            arrowParams.ToImmutableList(),
            arrayLiteral,
            returnType: BuildReadonlyUnknownArrayType());

        return new VariableDeclarationNode(
            name: new IdentifierNode($"get{hookBaseName}QueryKey"),
            declarationKind: VariableDeclarationKind.Const,
            type: null,
            initializer: arrow,
            isExported: true);
    }

    /// <summary>
    /// <c>export const use&lt;Op&gt; = (params, options?) =&gt; queryFn(...options...);</c>
    /// </summary>
    private DeclarationNode BuildUseQueryDeclaration(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        var optionsObj = BuildQueryOptionsObject(operation, hookBaseName, hasParams);
        var queryCallArg = WrapOptionsInGetter
            ? (ExpressionNode)new ArrowFunctionExpressionNode(
                ImmutableList<ParameterDeclarationNode>.Empty,
                optionsObj)
            : optionsObj;

        var queryCall = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode(QueryFnName)),
            ImmutableList.Create<ExpressionNode>(queryCallArg));

        var arrowParams = BuildHookParams(paramsTypeName, hasParams);
        var arrow = new ArrowFunctionExpressionNode(arrowParams, queryCall);

        return new VariableDeclarationNode(
            name: new IdentifierNode($"use{hookBaseName}"),
            declarationKind: VariableDeclarationKind.Const,
            type: null,
            initializer: arrow,
            isExported: true);
    }

    private ObjectLiteralExpressionNode BuildQueryOptionsObject(
        IrOperation operation,
        string hookBaseName,
        bool hasParams)
    {
        var queryKeyArgs = hasParams
            ? ImmutableList.Create<ExpressionNode>(new IdentifierExpressionNode(new IdentifierNode("params")))
            : ImmutableList<ExpressionNode>.Empty;

        var queryKeyCall = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode($"get{hookBaseName}QueryKey")),
            queryKeyArgs);

        var queryFnArrow = BuildClientCallArrow(operation, hasParams);

        var properties = new List<PropertyAssignmentNode>
        {
            new(new IdentifierNode("queryKey"), queryKeyCall),
            new(new IdentifierNode("queryFn"), queryFnArrow),
            new(new IdentifierNode("spread"), BuildOptionsSpread(), isSpread: true),
        };

        return new ObjectLiteralExpressionNode(properties.ToImmutableList(), preferMultilineFormat: true);
    }

    private DeclarationNode BuildUseMutationDeclaration(
        IrOperation operation,
        string hookBaseName,
        string? paramsTypeName,
        bool hasParams)
    {
        var optionsObj = BuildMutationOptionsObject(operation, paramsTypeName, hasParams);
        var callArg = WrapOptionsInGetter
            ? (ExpressionNode)new ArrowFunctionExpressionNode(
                ImmutableList<ParameterDeclarationNode>.Empty,
                optionsObj)
            : optionsObj;

        var mutationCall = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode(MutationFnName)),
            ImmutableList.Create<ExpressionNode>(callArg));

        var arrowParams = ImmutableList.Create(BuildOptionsParam());
        var arrow = new ArrowFunctionExpressionNode(arrowParams, mutationCall);

        return new VariableDeclarationNode(
            name: new IdentifierNode($"use{hookBaseName}"),
            declarationKind: VariableDeclarationKind.Const,
            type: null,
            initializer: arrow,
            isExported: true);
    }

    private ObjectLiteralExpressionNode BuildMutationOptionsObject(
        IrOperation operation,
        string? paramsTypeName,
        bool hasParams)
    {
        // mutationFn: (variables: Params) => methodName(variables)
        var variablesType = hasParams && paramsTypeName != null
            ? (TypeNode)BuildSimpleType(paramsTypeName)
            : new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true);

        var variableParam = new ParameterDeclarationNode(
            new IdentifierNode("variables"),
            variablesType,
            isRequired: true);

        var fnCallArgs = hasParams
            ? ImmutableList.Create<ExpressionNode>(new IdentifierExpressionNode(new IdentifierNode("variables")))
            : ImmutableList<ExpressionNode>.Empty;

        var fnCall = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode(operation.MethodName)),
            fnCallArgs);

        var mutationFnArrow = new ArrowFunctionExpressionNode(
            ImmutableList.Create(variableParam),
            fnCall);

        var properties = new List<PropertyAssignmentNode>
        {
            new(new IdentifierNode("mutationFn"), mutationFnArrow),
            new(new IdentifierNode("spread"), BuildOptionsSpread(), isSpread: true),
        };

        return new ObjectLiteralExpressionNode(properties.ToImmutableList(), preferMultilineFormat: true);
    }

    /// <summary>
    /// queryFn: () =&gt; methodName(params)
    /// (или methodName() для операций без параметров)
    /// </summary>
    private ArrowFunctionExpressionNode BuildClientCallArrow(IrOperation operation, bool hasParams)
    {
        var args = hasParams
            ? ImmutableList.Create<ExpressionNode>(new IdentifierExpressionNode(new IdentifierNode("params")))
            : ImmutableList<ExpressionNode>.Empty;

        var call = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode(operation.MethodName)),
            args);

        return new ArrowFunctionExpressionNode(
            ImmutableList<ParameterDeclarationNode>.Empty,
            call);
    }

    /// <summary>
    /// Spread выражение <c>...(options ?? {})</c> — безопасный спред для <c>object | undefined</c>.
    /// </summary>
    private static ExpressionNode BuildOptionsSpread()
    {
        var options = new IdentifierExpressionNode(new IdentifierNode("options"));
        var emptyObj = new ObjectLiteralExpressionNode(ImmutableList<PropertyAssignmentNode>.Empty);
        return new BinaryExpressionNode(options, BinaryOperator.NullishCoalescing, emptyObj);
    }

    private IReadOnlyList<ParameterDeclarationNode> BuildHookParams(string? paramsTypeName, bool hasParams)
    {
        var list = new List<ParameterDeclarationNode>();
        if (hasParams && paramsTypeName != null)
        {
            list.Add(new ParameterDeclarationNode(
                new IdentifierNode("params"),
                BuildSimpleType(paramsTypeName),
                isRequired: true));
        }
        list.Add(BuildOptionsParam());
        return list.ToImmutableList();
    }

    private static ParameterDeclarationNode BuildOptionsParam()
    {
        return new ParameterDeclarationNode(
            new IdentifierNode("options"),
            new SimpleTypeNode(new IdentifierNode("object"), isPrimitive: true),
            isRequired: false);
    }

    private static SimpleTypeNode BuildSimpleType(string name) =>
        new(new IdentifierNode(name), isPrimitive: false);

    private static TypeNode BuildReadonlyUnknownArrayType()
    {
        var unknown = new SimpleTypeNode(new IdentifierNode("unknown"), isPrimitive: true);
        return new ArrayTypeNode(unknown, isReadOnly: true);
    }
}
