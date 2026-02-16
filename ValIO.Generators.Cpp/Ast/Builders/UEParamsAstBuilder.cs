using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Nodes.UE;
using ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Ast.Builders;

/// <summary>
/// Строитель AST для Params-структур из IrOperation.
/// Создаёт контейнеры параметров с подструктурами Route/Query/Body/Form.
/// Если у операции только одна группа параметров — структура упрощается (без вложенности).
/// </summary>
public sealed class UEParamsAstBuilder
{
    private readonly UETypeMapper _typeMapper;
    private readonly UENamingConvention _naming;
    private readonly CppGenerationOptions _options;

    public UEParamsAstBuilder(UETypeMapper typeMapper, UENamingConvention naming, CppGenerationOptions options)
    {
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Строит CppHeaderFileNode с Params-структурами для операции
    /// </summary>
    public CppHeaderFileNode Build(IrOperation operation, IReadOnlySet<string> enumNames, string clientName)
    {
        var paramsName = _naming.ParamsStructName(operation.MethodName);
        var fileName = _naming.ParamsFileName(operation.MethodName);
        var category = _naming.OperationTypeCategory(_options.BlueprintCategory, clientName, operation.MethodName);

        var declarations = new List<AstNode>();
        var aggregateFields = new List<UPropertyFieldNode>();

        var hasRoute = operation.PathParameters.Count > 0;
        var hasQuery = operation.QueryParameters.Count > 0;
        var hasBody = operation.RequestBody != null &&
                      operation.RequestBody.ContentType is "application/json" or null;
        var hasForm = operation.RequestBody?.ContentType is "multipart/form-data" or "application/x-www-form-urlencoded";
        var hasHeaders = operation.HeaderParameters.Count > 0;

        var groupCount = (hasRoute ? 1 : 0) + (hasQuery ? 1 : 0) + (hasBody ? 1 : 0) + (hasForm ? 1 : 0) + (hasHeaders ? 1 : 0);

        var methodNamePascal = UENamingConvention.ToPascalCase(operation.MethodName);

        // Если одна группа или менее — упрощённая плоская структура
        if (groupCount <= 1)
        {
            var flatFields = BuildFlatFields(operation, category);
            var flatDoc = new CommentNode(DoxygenCommentBuilder.BuildParamsStructDocumentation(operation, paramsName, methodNamePascal, hasBody: hasBody || hasForm), CommentKind.MultiLine);
            var flatStruct = new UStructDeclarationNode(
                name: new IdentifierNode(paramsName),
                specifiers: new UStructSpecifiers { BlueprintType = true, Category = category },
                fields: flatFields,
                documentation: flatDoc);

            declarations.Add(flatStruct);
        }
        else
        {
            // Route под-структура
            if (hasRoute)
            {
                var routeName = _naming.RouteParamsStructName(operation.MethodName);
                var routeFields = BuildParameterFields(operation.PathParameters, category);
                var routeDoc = new CommentNode(DoxygenCommentBuilder.BuildRouteParamsDocumentation(operation, methodNamePascal), CommentKind.MultiLine);
                var routeStruct = new UStructDeclarationNode(
                    name: new IdentifierNode(routeName),
                    specifiers: new UStructSpecifiers { BlueprintType = true, Category = category },
                    fields: routeFields,
                    documentation: routeDoc);
                declarations.Add(routeStruct);

                aggregateFields.Add(new UPropertyFieldNode(
                    name: new IdentifierNode("Route"),
                    type: new Core.Ast.Nodes.Types.SimpleTypeNode(new IdentifierNode(routeName)),
                    specifiers: UPropertySpecifiers.ParamField(category),
                    documentation: new CommentNode("@brief Path/route parameters.\n@details Values are substituted into the URL path (e.g. /api/projects/{projectId}).\n@see Params.Route", CommentKind.MultiLine)));
            }

            // Query под-структура
            if (hasQuery)
            {
                var queryName = _naming.QueryParamsStructName(operation.MethodName);
                var queryFields = BuildParameterFields(operation.QueryParameters, category);
                var queryDoc = new CommentNode(DoxygenCommentBuilder.BuildQueryParamsDocumentation(operation, methodNamePascal), CommentKind.MultiLine);
                var queryStruct = new UStructDeclarationNode(
                    name: new IdentifierNode(queryName),
                    specifiers: new UStructSpecifiers { BlueprintType = true, Category = category },
                    fields: queryFields,
                    documentation: queryDoc);
                declarations.Add(queryStruct);

                aggregateFields.Add(new UPropertyFieldNode(
                    name: new IdentifierNode("Query"),
                    type: new Core.Ast.Nodes.Types.SimpleTypeNode(new IdentifierNode(queryName)),
                    specifiers: UPropertySpecifiers.ParamField(category),
                    documentation: new CommentNode("@brief Query string parameters.\n@details Appended to the URL as ?key=value pairs.\n@see Params.Query", CommentKind.MultiLine)));
            }

            // Headers под-структура
            if (hasHeaders)
            {
                var headerName = $"F{methodNamePascal}HeaderParams";
                var headerFields = BuildParameterFields(operation.HeaderParameters, category);
                var headerDoc = new CommentNode($"@brief HTTP header parameters for {methodNamePascal}.\n@see Params.Headers", CommentKind.MultiLine);
                var headerStruct = new UStructDeclarationNode(
                    name: new IdentifierNode(headerName),
                    specifiers: new UStructSpecifiers { BlueprintType = true, Category = category },
                    fields: headerFields,
                    documentation: headerDoc);
                declarations.Add(headerStruct);

                aggregateFields.Add(new UPropertyFieldNode(
                    name: new IdentifierNode("Headers"),
                    type: new Core.Ast.Nodes.Types.SimpleTypeNode(new IdentifierNode(headerName)),
                    specifiers: UPropertySpecifiers.ParamField(category),
                    documentation: new CommentNode("@brief HTTP header parameters.\n@see Params.Headers", CommentKind.MultiLine)));
            }

            // Body под-структура (JSON)
            if (hasBody && operation.RequestBody != null)
            {
                var bodyName = _naming.BodyParamsStructName(operation.MethodName);
                var bodyType = _typeMapper.MapType(operation.RequestBody.Type);
                var bodyDataDoc = new CommentNode(
                    string.IsNullOrWhiteSpace(operation.RequestBody.Description)
                        ? "@brief Request body (JSON).\n@details Sent in the HTTP request body."
                        : $"@brief Request body (JSON).\n@details Sent in the HTTP request body.\n\n{operation.RequestBody.Description}",
                    CommentKind.MultiLine);
                var bodyFields = new List<UPropertyFieldNode>
                {
                    new(
                        name: new IdentifierNode("Data"),
                        type: bodyType,
                        specifiers: UPropertySpecifiers.ParamField(category),
                        documentation: bodyDataDoc)
                };
                var bodyStructDoc = new CommentNode(DoxygenCommentBuilder.BuildBodyParamsDocumentation(operation.RequestBody.Description, methodNamePascal, "JSON request body"), CommentKind.MultiLine);
                var bodyStruct = new UStructDeclarationNode(
                    name: new IdentifierNode(bodyName),
                    specifiers: new UStructSpecifiers { BlueprintType = true, Category = category },
                    fields: bodyFields,
                    documentation: bodyStructDoc);
                declarations.Add(bodyStruct);

                aggregateFields.Add(new UPropertyFieldNode(
                    name: new IdentifierNode("Body"),
                    type: new Core.Ast.Nodes.Types.SimpleTypeNode(new IdentifierNode(bodyName)),
                    specifiers: UPropertySpecifiers.ParamField(category),
                    documentation: new CommentNode("@brief Request body (JSON).\n@details Sent in the HTTP request body.\n@see Params.Body", CommentKind.MultiLine)));
            }

            // Form под-структура (multipart)
            if (hasForm && operation.RequestBody != null)
            {
                var formName = _naming.FormParamsStructName(operation.MethodName);
                var formType = _typeMapper.MapType(operation.RequestBody.Type);
                var formDataDoc = new CommentNode(
                    string.IsNullOrWhiteSpace(operation.RequestBody.Description)
                        ? "@brief Form data (multipart/form-data).\n@details For file uploads and form submissions."
                        : $"@brief Form data (multipart/form-data).\n@details For file uploads and form submissions.\n\n{operation.RequestBody.Description}",
                    CommentKind.MultiLine);
                var formFields = new List<UPropertyFieldNode>
                {
                    new(
                        name: new IdentifierNode("Data"),
                        type: formType,
                        specifiers: UPropertySpecifiers.ParamField(category),
                        documentation: formDataDoc)
                };
                var formStructDoc = new CommentNode(DoxygenCommentBuilder.BuildBodyParamsDocumentation(operation.RequestBody.Description, methodNamePascal, "Form data (multipart/form-data)"), CommentKind.MultiLine);
                var formStruct = new UStructDeclarationNode(
                    name: new IdentifierNode(formName),
                    specifiers: new UStructSpecifiers { BlueprintType = true, Category = category },
                    fields: formFields,
                    documentation: formStructDoc);
                declarations.Add(formStruct);

                aggregateFields.Add(new UPropertyFieldNode(
                    name: new IdentifierNode("Form"),
                    type: new Core.Ast.Nodes.Types.SimpleTypeNode(new IdentifierNode(formName)),
                    specifiers: UPropertySpecifiers.ParamField(category),
                    documentation: new CommentNode("@brief Form data (multipart/form-data).\n@details For file uploads and form submissions.\n@see Params.Form", CommentKind.MultiLine)));
            }

            // Агрегирующая структура
            var aggregateDoc = new CommentNode(DoxygenCommentBuilder.BuildParamsStructDocumentation(operation, paramsName, methodNamePascal, hasBody: hasBody || hasForm), CommentKind.MultiLine);
            var aggregateStruct = new UStructDeclarationNode(
                name: new IdentifierNode(paramsName),
                specifiers: new UStructSpecifiers { BlueprintType = true, Category = category },
                fields: aggregateFields,
                documentation: aggregateDoc);
            declarations.Add(aggregateStruct);
        }

        var includes = new List<CppIncludeDirectiveNode>();
        if (_options.AddUnrealEngineIncludesInHeaders)
            includes.Add(new("CoreMinimal.h"));

        var referencedModels = new HashSet<string>();
        var referencedEnums = new HashSet<string>();
        if (operation.RequestBody?.Type != null)
            CollectTypeReferences(operation.RequestBody.Type, referencedModels, referencedEnums, enumNames);
        foreach (var p in operation.PathParameters)
            CollectTypeReferences(p.Type, referencedModels, referencedEnums, enumNames);
        foreach (var p in operation.QueryParameters)
            CollectTypeReferences(p.Type, referencedModels, referencedEnums, enumNames);
        foreach (var p in operation.HeaderParameters)
            CollectTypeReferences(p.Type, referencedModels, referencedEnums, enumNames);

        // Params в Public/Endpoints/{OpId}/, Models/Enums в Public/Models, Public/Enums
        foreach (var modelName in referencedModels.OrderBy(x => x))
            includes.Add(new CppIncludeDirectiveNode(_naming.ModelIncludePathFromEndpoint(modelName)));
        foreach (var enumName in referencedEnums.OrderBy(x => x))
            includes.Add(new CppIncludeDirectiveNode(_naming.EnumIncludePathFromEndpoint(enumName)));

        // .generated.h должен быть всегда последним (требование UHT)
        includes.Add(new CppIncludeDirectiveNode(fileName.Replace(".h", _options.GeneratedHeaderSuffix)));

        return new CppHeaderFileNode(
            fileName: fileName,
            headerComment: _options.Copyright != null ? new CommentNode(_options.Copyright, CommentKind.MultiLine) : null,
            includes: includes,
            declarations: declarations);
    }

    /// <summary>
    /// Строит плоский список полей для упрощённой структуры (одна группа параметров)
    /// </summary>
    private List<UPropertyFieldNode> BuildFlatFields(IrOperation operation, string category)
    {
        var fields = new List<UPropertyFieldNode>();

        foreach (var param in operation.PathParameters)
            fields.Add(BuildParamField(param, category));
        foreach (var param in operation.QueryParameters)
            fields.Add(BuildParamField(param, category));
        foreach (var param in operation.HeaderParameters)
            fields.Add(BuildParamField(param, category));

        if (operation.RequestBody != null)
        {
            var bodyDoc = new CommentNode(
                string.IsNullOrWhiteSpace(operation.RequestBody.Description)
                    ? "@brief Request body (JSON).\n@details Sent in the HTTP request body."
                    : $"@brief Request body (JSON).\n@details Sent in the HTTP request body.\n\n{operation.RequestBody.Description}",
                CommentKind.MultiLine);
            fields.Add(new UPropertyFieldNode(
                name: new IdentifierNode("Body"),
                type: _typeMapper.MapType(operation.RequestBody.Type),
                specifiers: UPropertySpecifiers.ParamField(category),
                documentation: bodyDoc));
        }

        return fields;
    }

    /// <summary>
    /// Строит список полей из списка IrParameter
    /// </summary>
    private List<UPropertyFieldNode> BuildParameterFields(IReadOnlyList<IrParameter> parameters, string category)
    {
        return parameters
            .Select(p => BuildParamField(p, category))
            .ToList();
    }

    /// <summary>
    /// Строит одно поле из IrParameter
    /// </summary>
    private UPropertyFieldNode BuildParamField(IrParameter param, string category)
    {
        var type = _typeMapper.MapType(param.Type);
        var defaultValueStr = _typeMapper.GetDefaultValue(param.Type);
        ExpressionNode? defaultValue = defaultValueStr != null
            ? new LiteralExpressionNode(defaultValueStr, LiteralKind.String)
            : null;

        return new UPropertyFieldNode(
            name: new IdentifierNode(_naming.PropertyName(param.Name)),
            type: type,
            specifiers: UPropertySpecifiers.ParamField(category),
            defaultValue: defaultValue,
            documentation: !string.IsNullOrWhiteSpace(param.Description)
                ? new CommentNode($"@brief {param.Description}", CommentKind.MultiLine) : null);
    }

    /// <summary>
    /// Рекурсивно собирает ссылки на модели и enum'ы из IrType
    /// </summary>
    private static void CollectTypeReferences(IrType type, HashSet<string> models, HashSet<string> enums, IReadOnlySet<string> enumNames)
    {
        switch (type)
        {
            case IrModelReference modelRef:
                if (enumNames.Contains(modelRef.ModelName))
                    enums.Add(modelRef.ModelName);
                else
                    models.Add(modelRef.ModelName);
                break;
            case IrArrayType array:
                CollectTypeReferences(array.ElementType, models, enums, enumNames);
                break;
            case IrDictionaryType dict:
                CollectTypeReferences(dict.KeyType, models, enums, enumNames);
                CollectTypeReferences(dict.ValueType, models, enums, enumNames);
                break;
            case IrNullableType nullable:
                CollectTypeReferences(nullable.InnerType, models, enums, enumNames);
                break;
            case IrOptionalType optional:
                CollectTypeReferences(optional.InnerType, models, enums, enumNames);
                break;
            case IrUnionType union:
                foreach (var t in union.Types) CollectTypeReferences(t, models, enums, enumNames);
                break;
            case IrIntersectionType intersection:
                foreach (var t in intersection.Types) CollectTypeReferences(t, models, enums, enumNames);
                break;
        }
    }
}
