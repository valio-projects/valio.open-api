using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
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
    public CppHeaderFileNode Build(IrOperation operation)
    {
        var paramsName = _naming.ParamsStructName(operation.MethodName);
        var fileName = _naming.ParamsFileName(operation.MethodName);
        var category = _options.BlueprintCategory;

        var declarations = new List<AstNode>();
        var aggregateFields = new List<UPropertyFieldNode>();

        var hasRoute = operation.PathParameters.Count > 0;
        var hasQuery = operation.QueryParameters.Count > 0;
        var hasBody = operation.RequestBody != null &&
                      operation.RequestBody.ContentType is "application/json" or null;
        var hasForm = operation.RequestBody?.ContentType is "multipart/form-data" or "application/x-www-form-urlencoded";
        var hasHeaders = operation.HeaderParameters.Count > 0;

        var groupCount = (hasRoute ? 1 : 0) + (hasQuery ? 1 : 0) + (hasBody ? 1 : 0) + (hasForm ? 1 : 0) + (hasHeaders ? 1 : 0);

        // Если одна группа или менее — упрощённая плоская структура
        if (groupCount <= 1)
        {
            var flatFields = BuildFlatFields(operation, category);
            var flatStruct = new UStructDeclarationNode(
                name: new IdentifierNode(paramsName),
                specifiers: new UStructSpecifiers { BlueprintType = true },
                fields: flatFields);

            declarations.Add(flatStruct);
        }
        else
        {
            // Route под-структура
            if (hasRoute)
            {
                var routeName = _naming.RouteParamsStructName(operation.MethodName);
                var routeFields = BuildParameterFields(operation.PathParameters, "Route");
                var routeStruct = new UStructDeclarationNode(
                    name: new IdentifierNode(routeName),
                    specifiers: new UStructSpecifiers { BlueprintType = true },
                    fields: routeFields);
                declarations.Add(routeStruct);

                aggregateFields.Add(new UPropertyFieldNode(
                    name: new IdentifierNode("Route"),
                    type: new Core.Ast.Nodes.Types.SimpleTypeNode(new IdentifierNode(routeName)),
                    specifiers: UPropertySpecifiers.ParamField("Route")));
            }

            // Query под-структура
            if (hasQuery)
            {
                var queryName = _naming.QueryParamsStructName(operation.MethodName);
                var queryFields = BuildParameterFields(operation.QueryParameters, "Query");
                var queryStruct = new UStructDeclarationNode(
                    name: new IdentifierNode(queryName),
                    specifiers: new UStructSpecifiers { BlueprintType = true },
                    fields: queryFields);
                declarations.Add(queryStruct);

                aggregateFields.Add(new UPropertyFieldNode(
                    name: new IdentifierNode("Query"),
                    type: new Core.Ast.Nodes.Types.SimpleTypeNode(new IdentifierNode(queryName)),
                    specifiers: UPropertySpecifiers.ParamField("Query")));
            }

            // Headers под-структура
            if (hasHeaders)
            {
                var headerName = $"F{UENamingConvention.ToPascalCase(operation.MethodName)}HeaderParams";
                var headerFields = BuildParameterFields(operation.HeaderParameters, "Headers");
                var headerStruct = new UStructDeclarationNode(
                    name: new IdentifierNode(headerName),
                    specifiers: new UStructSpecifiers { BlueprintType = true },
                    fields: headerFields);
                declarations.Add(headerStruct);

                aggregateFields.Add(new UPropertyFieldNode(
                    name: new IdentifierNode("Headers"),
                    type: new Core.Ast.Nodes.Types.SimpleTypeNode(new IdentifierNode(headerName)),
                    specifiers: UPropertySpecifiers.ParamField("Headers")));
            }

            // Body под-структура (JSON)
            if (hasBody && operation.RequestBody != null)
            {
                var bodyName = _naming.BodyParamsStructName(operation.MethodName);
                var bodyType = _typeMapper.MapType(operation.RequestBody.Type);
                var bodyFields = new List<UPropertyFieldNode>
                {
                    new(
                        name: new IdentifierNode("Data"),
                        type: bodyType,
                        specifiers: UPropertySpecifiers.ParamField("Body"))
                };
                var bodyStruct = new UStructDeclarationNode(
                    name: new IdentifierNode(bodyName),
                    specifiers: new UStructSpecifiers { BlueprintType = true },
                    fields: bodyFields);
                declarations.Add(bodyStruct);

                aggregateFields.Add(new UPropertyFieldNode(
                    name: new IdentifierNode("Body"),
                    type: new Core.Ast.Nodes.Types.SimpleTypeNode(new IdentifierNode(bodyName)),
                    specifiers: UPropertySpecifiers.ParamField("Body")));
            }

            // Form под-структура (multipart)
            if (hasForm && operation.RequestBody != null)
            {
                var formName = _naming.FormParamsStructName(operation.MethodName);
                var formType = _typeMapper.MapType(operation.RequestBody.Type);
                var formFields = new List<UPropertyFieldNode>
                {
                    new(
                        name: new IdentifierNode("Data"),
                        type: formType,
                        specifiers: UPropertySpecifiers.ParamField("Form"))
                };
                var formStruct = new UStructDeclarationNode(
                    name: new IdentifierNode(formName),
                    specifiers: new UStructSpecifiers { BlueprintType = true },
                    fields: formFields);
                declarations.Add(formStruct);

                aggregateFields.Add(new UPropertyFieldNode(
                    name: new IdentifierNode("Form"),
                    type: new Core.Ast.Nodes.Types.SimpleTypeNode(new IdentifierNode(formName)),
                    specifiers: UPropertySpecifiers.ParamField("Form")));
            }

            // Агрегирующая структура
            var aggregateStruct = new UStructDeclarationNode(
                name: new IdentifierNode(paramsName),
                specifiers: new UStructSpecifiers { BlueprintType = true },
                fields: aggregateFields);
            declarations.Add(aggregateStruct);
        }

        var includes = new List<CppIncludeDirectiveNode>
        {
            new("CoreMinimal.h"),
            new(fileName.Replace(".h", _options.GeneratedHeaderSuffix))
        };

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
            fields.Add(new UPropertyFieldNode(
                name: new IdentifierNode("Body"),
                type: _typeMapper.MapType(operation.RequestBody.Type),
                specifiers: UPropertySpecifiers.ParamField(category)));
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
                ? new CommentNode(param.Description, CommentKind.MultiLine) : null);
    }
}
