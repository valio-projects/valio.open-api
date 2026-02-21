using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Generators.TypeScript.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Строитель AST для Params-интерфейсов из IrOperation.
/// Создаёт интерфейсы параметров с под-интерфейсами Route/Query/Body/Form (по аналогии с UE Cpp).
/// При одной группе параметров — плоская структура без вложенности.
/// </summary>
public sealed class TypeScriptParamsAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly Func<IrType, TypeNode> _typeBuilder;

    public TypeScriptParamsAstBuilder(
        INamingConverter namingConverter,
        Func<IrType, TypeNode> typeBuilder)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeBuilder = typeBuilder ?? throw new ArgumentNullException(nameof(typeBuilder));
    }

    /// <summary>
    /// Имя params интерфейса: {OperationName}Params
    /// </summary>
    public string ParamsInterfaceName(string operationName)
    {
        return $"{_namingConverter.ToPascalCase(operationName)}Params";
    }

    /// <summary>
    /// Имя Route params интерфейса: {OperationName}RouteParams
    /// </summary>
    public string RouteParamsInterfaceName(string operationName)
    {
        return $"{_namingConverter.ToPascalCase(operationName)}RouteParams";
    }

    /// <summary>
    /// Имя Query params интерфейса: {OperationName}QueryParams
    /// </summary>
    public string QueryParamsInterfaceName(string operationName)
    {
        return $"{_namingConverter.ToPascalCase(operationName)}QueryParams";
    }

    /// <summary>
    /// Имя Body params типа (используется существующий тип из RequestBody)
    /// </summary>
    public string BodyParamsTypeName(IrRequestBody body)
    {
        return body.Type switch
        {
            IrModelReference modelRef => modelRef.ModelName,
            _ => "unknown"
        };
    }

    /// <summary>
    /// Строит SourceFileNode с Params-интерфейсами для операции
    /// </summary>
    public SourceFileNode BuildParamsSourceFile(
        IrOperation operation,
        string? importPath,
        FileOrganization? fileOrganization,
        string currentOperationName)
    {
        var declarations = new List<DeclarationNode>();
        var imports = new List<ImportNode>();

        var hasRoute = operation.PathParameters.Count > 0;
        var hasQuery = operation.QueryParameters.Count > 0;
        var rb = operation.RequestBody;
        var hasBody = rb != null && rb.IsJson;
        var hasForm = rb != null && rb.IsForm;
        var hasHeaders = operation.HeaderParameters.Count > 0;

        var groupCount = (hasRoute ? 1 : 0) + (hasQuery ? 1 : 0) + (hasBody ? 1 : 0) + (hasForm ? 1 : 0) + (hasHeaders ? 1 : 0);

        // Route под-интерфейс
        if (hasRoute)
        {
            var routeInterface = BuildRouteParamsInterface(operation);
            declarations.Add(routeInterface);
        }

        // Query под-интерфейс
        if (hasQuery)
        {
            var queryInterface = BuildQueryParamsInterface(operation);
            declarations.Add(queryInterface);
        }

        // Headers под-интерфейс
        if (hasHeaders)
        {
            var headersInterface = BuildHeadersParamsInterface(operation);
            declarations.Add(headersInterface);
        }

        // Агрегирующий params интерфейс
        var paramsInterface = groupCount <= 1
            ? BuildFlatParamsInterface(operation, hasBody, hasForm)
            : BuildAggregateParamsInterface(operation, hasRoute, hasQuery, hasBody, hasForm, hasHeaders);

        declarations.Add(paramsInterface);

        // Импорты для типов body/form (относительный путь из папки операции)
        if (hasBody || hasForm)
        {
            var bodyTypeName = BodyParamsTypeName(rb!);
            if (bodyTypeName != "unknown")
            {
                var modelFileName = _namingConverter.ToCamelCase(bodyTypeName);
                var relativePath = $"../../{fileOrganization?.ModelsPath ?? "models"}/{modelFileName}/{modelFileName}";
                imports.Add(new ImportNode(
                    items: new[] { new IdentifierNode(bodyTypeName) }.ToImmutableList(),
                    from: relativePath,
                    kind: ImportKind.Named));
            }
        }

        var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();

        return new SourceFileNode(
            imports: imports.ToImmutableList(),
            exports: exports,
            declarations: declarations.ToImmutableList());
    }

    private InterfaceDeclarationNode BuildRouteParamsInterface(IrOperation operation)
    {
        var routeName = RouteParamsInterfaceName(operation.MethodName);
        var properties = operation.PathParameters
            .Select(p => new PropertySignatureNode(
                name: new IdentifierNode(p.Name),
                type: _typeBuilder(p.Type),
                isRequired: true,
                documentation: !string.IsNullOrWhiteSpace(p.Description)
                    ? new CommentNode(p.Description, CommentKind.JSDoc)
                    : null) as DeclarationNode)
            .ToImmutableList();

        return new InterfaceDeclarationNode(
            name: new IdentifierNode(routeName),
            accessModifier: AccessModifier.Public,
            baseTypes: ImmutableList<TypeNode>.Empty,
            members: properties,
            documentation: new CommentNode($"Route/path parameters for {operation.MethodName}", CommentKind.JSDoc));
    }

    private InterfaceDeclarationNode BuildQueryParamsInterface(IrOperation operation)
    {
        var queryName = QueryParamsInterfaceName(operation.MethodName);
        var properties = operation.QueryParameters
            .Select(p => new PropertySignatureNode(
                name: new IdentifierNode(p.Name),
                type: _typeBuilder(p.Type),
                isRequired: p.IsRequired,
                documentation: !string.IsNullOrWhiteSpace(p.Description)
                    ? new CommentNode(p.Description, CommentKind.JSDoc)
                    : null) as DeclarationNode)
            .ToImmutableList();

        return new InterfaceDeclarationNode(
            name: new IdentifierNode(queryName),
            accessModifier: AccessModifier.Public,
            baseTypes: ImmutableList<TypeNode>.Empty,
            members: properties,
            documentation: new CommentNode($"Query string parameters for {operation.MethodName}", CommentKind.JSDoc));
    }

    private InterfaceDeclarationNode BuildHeadersParamsInterface(IrOperation operation)
    {
        var headersName = $"{_namingConverter.ToPascalCase(operation.MethodName)}HeaderParams";
        var properties = operation.HeaderParameters
            .Select(p => new PropertySignatureNode(
                name: new IdentifierNode(p.Name),
                type: _typeBuilder(p.Type),
                isRequired: p.IsRequired,
                documentation: !string.IsNullOrWhiteSpace(p.Description)
                    ? new CommentNode(p.Description, CommentKind.JSDoc)
                    : null) as DeclarationNode)
            .ToImmutableList();

        return new InterfaceDeclarationNode(
            name: new IdentifierNode(headersName),
            accessModifier: AccessModifier.Public,
            baseTypes: ImmutableList<TypeNode>.Empty,
            members: properties,
            documentation: new CommentNode($"Header parameters for {operation.MethodName}", CommentKind.JSDoc));
    }

    private InterfaceDeclarationNode BuildFlatParamsInterface(IrOperation operation, bool hasBody, bool hasForm)
    {
        var paramsName = ParamsInterfaceName(operation.MethodName);
        var properties = new List<DeclarationNode>();

        foreach (var param in operation.PathParameters)
        {
            properties.Add(new PropertySignatureNode(
                name: new IdentifierNode(param.Name),
                type: _typeBuilder(param.Type),
                isRequired: true));
        }
        foreach (var param in operation.QueryParameters)
        {
            properties.Add(new PropertySignatureNode(
                name: new IdentifierNode(param.Name),
                type: _typeBuilder(param.Type),
                isRequired: param.IsRequired));
        }
        foreach (var param in operation.HeaderParameters)
        {
            properties.Add(new PropertySignatureNode(
                name: new IdentifierNode(param.Name),
                type: _typeBuilder(param.Type),
                isRequired: param.IsRequired));
        }

        if (hasBody && operation.RequestBody != null)
        {
            var bodyType = _typeBuilder(operation.RequestBody.Type);
            properties.Add(new PropertySignatureNode(
                name: new IdentifierNode("body"),
                type: bodyType,
                isRequired: operation.RequestBody.IsRequired,
                documentation: new CommentNode("Request body (JSON)", CommentKind.JSDoc)));
        }

        if (hasForm && operation.RequestBody != null)
        {
            var formType = _typeBuilder(operation.RequestBody.Type);
            properties.Add(new PropertySignatureNode(
                name: new IdentifierNode("form"),
                type: formType,
                isRequired: operation.RequestBody.IsRequired,
                documentation: new CommentNode("Form data (multipart/form-data)", CommentKind.JSDoc)));
        }

        return new InterfaceDeclarationNode(
            name: new IdentifierNode(paramsName),
            accessModifier: AccessModifier.Public,
            baseTypes: ImmutableList<TypeNode>.Empty,
            members: properties.ToImmutableList(),
            documentation: new CommentNode($"Parameters for {operation.MethodName}", CommentKind.JSDoc));
    }

    private InterfaceDeclarationNode BuildAggregateParamsInterface(
        IrOperation operation,
        bool hasRoute,
        bool hasQuery,
        bool hasBody,
        bool hasForm,
        bool hasHeaders)
    {
        var paramsName = ParamsInterfaceName(operation.MethodName);
        var properties = new List<DeclarationNode>();

        if (hasRoute)
        {
            var routeName = RouteParamsInterfaceName(operation.MethodName);
            properties.Add(new PropertySignatureNode(
                name: new IdentifierNode("route"),
                type: new SimpleTypeNode(new IdentifierNode(routeName), isPrimitive: false, isNullable: false, isOptional: false),
                isRequired: true,
                documentation: new CommentNode("Path/route parameters", CommentKind.JSDoc)));
        }

        if (hasQuery)
        {
            var queryName = QueryParamsInterfaceName(operation.MethodName);
            var allQueryParamsOptional = operation.QueryParameters.All(p => !p.IsRequired);
            properties.Add(new PropertySignatureNode(
                name: new IdentifierNode("query"),
                type: new SimpleTypeNode(new IdentifierNode(queryName), isPrimitive: false, isNullable: false, isOptional: false),
                isRequired: !allQueryParamsOptional,
                documentation: new CommentNode("Query string parameters", CommentKind.JSDoc)));
        }

        if (hasHeaders)
        {
            var headersName = $"{_namingConverter.ToPascalCase(operation.MethodName)}HeaderParams";
            properties.Add(new PropertySignatureNode(
                name: new IdentifierNode("headers"),
                type: new SimpleTypeNode(new IdentifierNode(headersName), isPrimitive: false, isNullable: false, isOptional: false),
                isRequired: false,
                documentation: new CommentNode("HTTP header parameters", CommentKind.JSDoc)));
        }

        if (hasBody && operation.RequestBody != null)
        {
            var bodyType = _typeBuilder(operation.RequestBody.Type);
            properties.Add(new PropertySignatureNode(
                name: new IdentifierNode("body"),
                type: bodyType,
                isRequired: operation.RequestBody.IsRequired,
                documentation: new CommentNode("Request body (JSON)", CommentKind.JSDoc)));
        }

        if (hasForm && operation.RequestBody != null)
        {
            var formType = _typeBuilder(operation.RequestBody.Type);
            properties.Add(new PropertySignatureNode(
                name: new IdentifierNode("form"),
                type: formType,
                isRequired: operation.RequestBody.IsRequired,
                documentation: new CommentNode("Form data (multipart/form-data)", CommentKind.JSDoc)));
        }

        return new InterfaceDeclarationNode(
            name: new IdentifierNode(paramsName),
            accessModifier: ValIO.Core.Ast.Enums.AccessModifier.Public,
            baseTypes: ImmutableList<TypeNode>.Empty,
            members: properties.ToImmutableList(),
            documentation: new CommentNode($"Parameters for {operation.MethodName}", ValIO.Core.Ast.Enums.CommentKind.JSDoc));
    }

    /// <summary>
    /// Проверяет, имеет ли операция параметры (route, query, body, form, headers)
    /// </summary>
    public bool HasParameters(IrOperation operation)
    {
        return operation.PathParameters.Count > 0
            || operation.QueryParameters.Count > 0
            || operation.HeaderParameters.Count > 0
            || operation.RequestBody != null;
    }
}
