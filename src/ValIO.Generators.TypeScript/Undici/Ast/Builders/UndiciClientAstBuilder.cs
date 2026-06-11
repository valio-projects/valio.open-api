using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.JsDoc;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.TypeBox.Ast.Builders;
using ValIO.Generators.TypeScript.Valibot.Ast.Builders;

namespace ValIO.Generators.TypeScript.Undici.Ast.Builders;

/// <summary>
/// Builds Undici operation clients as TypeScript AST.
/// Generates one <c>{op}.undici.ts</c> per IrOperation.
/// </summary>
public sealed class UndiciClientAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptParamsAstBuilder _paramsBuilder;
    private readonly TypeScriptResultsAstBuilder _resultsBuilder;
    private readonly EndpointValidationLibrary _validationLibrary;
    private readonly TypeBoxAstBuilder _typeBoxAstBuilder;
    private readonly ValibotAstBuilder _valibotAstBuilder;
    private readonly string _uuidFormat;

    public UndiciClientAstBuilder(INamingConverter namingConverter, TypeScriptGenerationOptions? options)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _validationLibrary = EndpointValidationLibrary.From(options);
        _uuidFormat = TypeScriptOptionIds.UuidFormat.Resolve(options?.UuidFormat);
        _typeBoxAstBuilder = new TypeBoxAstBuilder(options?.UuidFormat);
        _valibotAstBuilder = new ValibotAstBuilder(options?.UuidFormat);

        var typeBuilder = new TypeScriptAstBuilder(namingConverter, options);
        _paramsBuilder = new TypeScriptParamsAstBuilder(namingConverter, typeBuilder.BuildType);
        _resultsBuilder = new TypeScriptResultsAstBuilder(namingConverter, typeBuilder.BuildType, options?.ResultTypeSuffix);
    }

    public SourceFileNode BuildOperationClientSourceFile(IrOperation operation, FileOrganization fileOrganization)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var operationFileName = _namingConverter.ToCamelCase(operation.MethodName);
        var hasParameters = _paramsBuilder.HasParameters(operation);
        var hasResultType = _resultsBuilder.HasResponsesWithBody(operation);
        var declarations = ImmutableList.CreateBuilder<DeclarationNode>();

        if (_validationLibrary.IsEnabled)
            AddParameterSchemaDeclarations(operation, declarations);

        declarations.Add(BuildOperationFunction(operation, hasParameters, hasResultType));

        return new SourceFileNode(
            imports: BuildImports(operation, operationFileName, fileOrganization, hasParameters, hasResultType, declarations.Count > 1),
            declarations: declarations.ToImmutable(),
            fileDocumentation: TypeScriptDoc.FileHeader(
                $"{operationFileName}.undici.ts",
                $"endpoints/{operationFileName}",
                $"Undici HTTP client for the {operation.MethodName} operation."));
    }

    private IReadOnlyList<ImportNode> BuildImports(
        IrOperation operation,
        string operationFileName,
        FileOrganization fileOrganization,
        bool hasParameters,
        bool hasResultType,
        bool hasLocalValidationSchemas)
    {
        var imports = ImmutableList.CreateBuilder<ImportNode>();
        imports.Add(new ImportNode(
            items: ImmutableList.Create(new IdentifierNode("request")),
            from: "undici",
            kind: ImportKind.Named));

        AddValidationRuntimeImports(imports, hasLocalValidationSchemas);

        if (hasParameters)
        {
            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(_paramsBuilder.ParamsInterfaceName(operation.MethodName))),
                from: $"./{operationFileName}.params",
                kind: ImportKind.Named,
                isTypeOnly: true));
        }

        if (hasResultType)
        {
            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(_resultsBuilder.ResultTypeName(operation.MethodName))),
                from: $"./{operationFileName}.results",
                kind: ImportKind.Named,
                isTypeOnly: true));
        }

        if (_validationLibrary.IsEnabled)
        {
            foreach (var schemaName in ExtractModelSchemaNames(operation))
            {
                imports.Add(new ImportNode(
                    items: ImmutableList.Create(new IdentifierNode(schemaName)),
                    from: BuildSchemaImportPath(schemaName[..^"Schema".Length], fileOrganization),
                    kind: ImportKind.Named));
            }

            AddUuidFormatImport(imports, operation, fileOrganization);
        }

        return imports.ToImmutable();
    }

    private void AddValidationRuntimeImports(ImmutableList<ImportNode>.Builder imports, bool hasLocalValidationSchemas)
    {
        if (!_validationLibrary.IsEnabled)
            return;

        switch (_validationLibrary.Kind)
        {
            case EndpointValidationKind.Zod when hasLocalValidationSchemas:
                imports.Add(new ImportNode(
                    items: ImmutableList.Create(new IdentifierNode("z")),
                    from: _validationLibrary.ZodImportPath,
                    kind: ImportKind.Named));
                break;
            case EndpointValidationKind.Valibot:
                imports.Add(new ImportNode(
                    items: ImmutableList.Create(new IdentifierNode("v")),
                    from: "valibot",
                    kind: ImportKind.Namespace));
                break;
            case EndpointValidationKind.TypeBox:
                imports.Add(new ImportNode(
                    items: ImmutableList.Create(new IdentifierNode("Type")),
                    from: "@sinclair/typebox",
                    kind: ImportKind.Named));
                imports.Add(new ImportNode(
                    items: ImmutableList.Create(new IdentifierNode("Value")),
                    from: "@sinclair/typebox/value",
                    kind: ImportKind.Named));
                break;
        }
    }

    private void AddParameterSchemaDeclarations(
        IrOperation operation,
        ImmutableList<DeclarationNode>.Builder declarations)
    {
        var opName = _namingConverter.ToPascalCase(operation.MethodName);

        if (operation.PathParameters.Count > 0)
        {
            declarations.Add(new VariableDeclarationNode(
                name: new IdentifierNode($"{opName}RouteParamsSchema"),
                declarationKind: VariableDeclarationKind.Const,
                initializer: BuildParameterObjectSchema(operation.PathParameters),
                isExported: false));
        }

        if (operation.QueryParameters.Count > 0)
        {
            declarations.Add(new VariableDeclarationNode(
                name: new IdentifierNode($"{opName}QueryParamsSchema"),
                declarationKind: VariableDeclarationKind.Const,
                initializer: BuildParameterObjectSchema(operation.QueryParameters),
                isExported: false));
        }
    }

    private FunctionDeclarationNode BuildOperationFunction(
        IrOperation operation,
        bool hasParameters,
        bool hasResultType)
    {
        var returnType = hasResultType
            ? Simple(_resultsBuilder.ResultTypeName(operation.MethodName))
            : Simple("void", isPrimitive: true);

        var parameters = ImmutableList.CreateBuilder<ParameterDeclarationNode>();
        if (hasParameters)
        {
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("params"),
                type: Simple(_paramsBuilder.ParamsInterfaceName(operation.MethodName)),
                isRequired: true));
        }

        parameters.Add(new ParameterDeclarationNode(
            name: new IdentifierNode("signal"),
            type: Simple("AbortSignal"),
            isRequired: false));

        return new FunctionDeclarationNode(
            name: new IdentifierNode(operation.MethodName),
            returnType: new GenericTypeNode(new IdentifierNode("Promise"), ImmutableList.Create(returnType)),
            parameters: parameters.ToImmutable(),
            body: BuildOperationFunctionBody(operation, hasResultType),
            isAsync: true,
            documentation: BuildDocumentation(operation),
            isExported: true);
    }

    private BlockNode BuildOperationFunctionBody(IrOperation operation, bool hasResultType)
    {
        var statements = new List<StatementNode>();
        var sources = BuildParameterSources(operation);

        if (_validationLibrary.IsEnabled)
            AddValidationParsingStatements(operation, sources, statements);

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("requestUrl"),
            type: null,
            initializer: BuildUrlExpression(operation, sources)));
        statements.Add(new BlankLineStatementNode());

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("requestOptions"),
            type: null,
            initializer: BuildUndiciOptionsExpression(operation, sources)));
        statements.Add(new BlankLineStatementNode());

        var requestCall = new CallExpressionNode(
            Id("request"),
            ImmutableList.Create<ExpressionNode>(Id("requestUrl"), Id("requestOptions")));

        if (!hasResultType)
        {
            statements.Add(new ExpressionStatementNode(new AwaitExpressionNode(requestCall)));
            return new BlockNode(statements.ToImmutableList());
        }

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("response"),
            type: null,
            initializer: new AwaitExpressionNode(requestCall)));

        // responseData = response.statusCode === 204 ? undefined : await response.body.json() as unknown
        var bodyJsonCall = new CallExpressionNode(
            Member(Member(Id("response"), "body"), "json"),
            ImmutableList<ExpressionNode>.Empty);
        var bodyJsonCast = new CastExpressionNode(
            Simple("unknown", isPrimitive: true),
            new AwaitExpressionNode(bodyJsonCall),
            useAsOperator: true);

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("responseData"),
            type: null,
            initializer: new ConditionalExpressionNode(
                new BinaryExpressionNode(
                    Member(Id("response"), "statusCode"),
                    BinaryOperator.Equal,
                    Number(204)),
                Id("undefined"),
                bodyJsonCast)));
        statements.Add(new BlankLineStatementNode());

        AddStatusResultBranches(operation, statements);
        statements.Add(BuildFallbackResult(operation));

        return new BlockNode(statements.ToImmutableList());
    }

    private void AddValidationParsingStatements(
        IrOperation operation,
        ParameterSources sources,
        List<StatementNode> statements)
    {
        var opName = _namingConverter.ToPascalCase(operation.MethodName);

        if (operation.PathParameters.Count > 0 && sources.RouteSource != null)
        {
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("parsedRoute"),
                type: null,
                initializer: BuildParseCall($"{opName}RouteParamsSchema", sources.RouteSource)));
            sources.RouteSource = Id("parsedRoute");
        }

        if (operation.QueryParameters.Count > 0 && sources.QuerySource != null)
        {
            var queryInput = sources.QuerySourceMayBeUndefined
                ? new BinaryExpressionNode(sources.QuerySource, BinaryOperator.NullishCoalescing, EmptyObject())
                : sources.QuerySource;

            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("parsedQuery"),
                type: null,
                initializer: BuildParseCall($"{opName}QueryParamsSchema", queryInput)));
            sources.QuerySource = Id("parsedQuery");
            sources.QuerySourceMayBeUndefined = false;
        }

        if (operation.RequestBody?.Type is IrModelReference bodyModelRef && sources.BodySource != null)
        {
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("parsedBody"),
                type: null,
                initializer: BuildParseCall($"{bodyModelRef.ModelName}Schema", sources.BodySource)));
            sources.BodySource = Id("parsedBody");
            sources.BodySourceMayBeUndefined = false;
        }

        if (statements.Count > 0)
            statements.Add(new BlankLineStatementNode());
    }

    private ObjectLiteralExpressionNode BuildUndiciOptionsExpression(
        IrOperation operation,
        ParameterSources sources)
    {
        var properties = new List<PropertyAssignmentNode>
        {
            new(new IdentifierNode("method"), String(operation.HttpMethod.ToUpperInvariant()))
        };

        if (operation.HeaderParameters.Count > 0 && sources.HeadersSource != null)
        {
            // *HeaderParams interface is narrow; undici accepts IncomingHttpHeaders.
            var castedHeaders = new CastExpressionNode(
                Simple("Record<string, string>", isPrimitive: false),
                new CastExpressionNode(Simple("unknown", isPrimitive: true), sources.HeadersSource, useAsOperator: true),
                useAsOperator: true);

            if (sources.HeadersSourceMayBeUndefined)
            {
                properties.Add(SpreadProperty(new ConditionalExpressionNode(
                    sources.HeadersSource,
                    Object(("headers", castedHeaders)),
                    EmptyObject())));
            }
            else
            {
                properties.Add(new PropertyAssignmentNode(new IdentifierNode("headers"), castedHeaders));
            }
        }

        if (operation.RequestBody != null && sources.BodySource != null)
        {
            // Undici accepts string | Buffer | Readable | FormData for body.
            // For JSON we serialize via JSON.stringify and set content-type header.
            ExpressionNode bodyExpression;
            if (operation.RequestBody.IsJson)
            {
                bodyExpression = new CallExpressionNode(
                    Member(Id("JSON"), "stringify"),
                    ImmutableList.Create<ExpressionNode>(sources.BodySource));
            }
            else
            {
                bodyExpression = new CastExpressionNode(
                    Simple("string", isPrimitive: true),
                    new CastExpressionNode(Simple("unknown", isPrimitive: true), sources.BodySource, useAsOperator: true),
                    useAsOperator: true);
            }

            if (sources.BodySourceMayBeUndefined)
            {
                properties.Add(SpreadProperty(new ConditionalExpressionNode(
                    new BinaryExpressionNode(sources.BodySource, BinaryOperator.NotEqual, Id("undefined")),
                    Object(("body", bodyExpression)),
                    EmptyObject())));
            }
            else
            {
                properties.Add(new PropertyAssignmentNode(new IdentifierNode("body"), bodyExpression));
            }
        }

        if (operation.QueryParameters.Count > 0 && sources.QuerySource != null)
        {
            properties.Add(new PropertyAssignmentNode(
                new IdentifierNode("query"),
                BuildQueryObject(operation, sources)));
        }

        properties.Add(SpreadProperty(new ConditionalExpressionNode(
            Id("signal"),
            Object(("signal", Id("signal"))),
            EmptyObject())));

        return new ObjectLiteralExpressionNode(properties.ToImmutableList(), preferMultilineFormat: true);
    }

    private ExpressionNode BuildQueryObject(IrOperation operation, ParameterSources sources)
    {
        var properties = new List<PropertyAssignmentNode>();

        foreach (var parameter in operation.QueryParameters)
        {
            var access = BuildParameterAccess(sources.QuerySource!, parameter.Name, sources.QuerySourceMayBeUndefined);
            if (parameter.IsRequired)
            {
                properties.Add(new PropertyAssignmentNode(new IdentifierNode(parameter.Name), access));
            }
            else
            {
                properties.Add(SpreadProperty(new ConditionalExpressionNode(
                    new BinaryExpressionNode(access, BinaryOperator.NotEqual, Id("undefined")),
                    Object((parameter.Name, access)),
                    EmptyObject())));
            }
        }

        var literal = new ObjectLiteralExpressionNode(properties.ToImmutableList(), preferMultilineFormat: true);
        return new CastExpressionNode(
            Simple("Record<string, string | number | boolean | string[]>", isPrimitive: false),
            new CastExpressionNode(Simple("unknown", isPrimitive: true), literal, useAsOperator: true),
            useAsOperator: true);
    }

    private void AddStatusResultBranches(IrOperation operation, List<StatementNode> statements)
    {
        var resultTypeName = _resultsBuilder.ResultTypeName(operation.MethodName);
        var responseStatus = Member(Id("response"), "statusCode");
        var responseData = Id("responseData");

        foreach (var response in operation.Responses.Where(r => r.Type != null && int.TryParse(r.StatusCode, out _)))
        {
            var statusCode = int.Parse(response.StatusCode);
            var isSuccessful = statusCode >= 200 && statusCode < 300;
            var body = response.Type is IrModelReference modelRef && _validationLibrary.IsEnabled
                ? BuildParseCall($"{modelRef.ModelName}Schema", responseData)
                : (ExpressionNode)responseData;

            var returnObject = new CastExpressionNode(
                Simple(resultTypeName),
                Object(
                    ("status", Number(statusCode)),
                    ("body", body),
                    ("isSuccessful", Bool(isSuccessful)),
                    ("isFailed", Bool(!isSuccessful))),
                useAsOperator: true);

            statements.Add(new IfStatementNode(
                new BinaryExpressionNode(responseStatus, BinaryOperator.Equal, Number(statusCode)),
                new BlockNode(ImmutableList.Create<StatementNode>(new ReturnStatementNode(returnObject)))));
        }
    }

    private ReturnStatementNode BuildFallbackResult(IrOperation operation)
    {
        var resultTypeName = _resultsBuilder.ResultTypeName(operation.MethodName);
        var responseStatus = Member(Id("response"), "statusCode");
        var successful = new BinaryExpressionNode(
            new BinaryExpressionNode(responseStatus, BinaryOperator.GreaterThanOrEqual, Number(200)),
            BinaryOperator.LogicalAnd,
            new BinaryExpressionNode(responseStatus, BinaryOperator.LessThan, Number(300)));

        return new ReturnStatementNode(new CastExpressionNode(
            Simple(resultTypeName),
            Object(
                ("status", responseStatus),
                ("body", Id("responseData")),
                ("isSuccessful", successful),
                ("isFailed", new UnaryExpressionNode(UnaryOperator.LogicalNot, successful))),
            useAsOperator: true));
    }

    private ExpressionNode BuildUrlExpression(IrOperation operation, ParameterSources sources)
    {
        if (operation.PathParameters.Count == 0 || sources.RouteSource == null)
            return String(operation.Path);

        var parts = new List<TemplateLiteralPart>();
        var cursor = 0;
        foreach (var parameter in operation.PathParameters)
        {
            var token = $"{{{parameter.Name}}}";
            // Casing плейсхолдера в пути может отличаться от имени параметра — сопоставляем без учёта регистра.
            var index = operation.Path.IndexOf(token, cursor, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;

            if (index > cursor)
                parts.Add(new TemplateLiteralPart(operation.Path[cursor..index]));
            parts.Add(new TemplateLiteralPart(BuildParameterAccess(sources.RouteSource, parameter.Name, sourceMayBeUndefined: false)));
            cursor = index + token.Length;
        }

        if (cursor < operation.Path.Length)
            parts.Add(new TemplateLiteralPart(operation.Path[cursor..]));

        return parts.Count == 0 ? String(operation.Path) : new TemplateLiteralExpressionNode(parts.ToImmutableList());
    }

    private ParameterSources BuildParameterSources(IrOperation operation)
    {
        var hasRoute = operation.PathParameters.Count > 0;
        var hasQuery = operation.QueryParameters.Count > 0;
        var hasHeaders = operation.HeaderParameters.Count > 0;
        var hasBody = operation.RequestBody != null;
        var groupCount = (hasRoute ? 1 : 0) + (hasQuery ? 1 : 0) + (hasHeaders ? 1 : 0) + (hasBody ? 1 : 0);
        var isFlat = groupCount <= 1;
        var paramsExpr = Id("params");

        return new ParameterSources
        {
            RouteSource = hasRoute ? isFlat ? paramsExpr : Member(paramsExpr, "route") : null,
            QuerySource = hasQuery ? isFlat ? paramsExpr : Member(paramsExpr, "query") : null,
            QuerySourceMayBeUndefined = hasQuery && !isFlat && operation.QueryParameters.All(p => !p.IsRequired),
            HeadersSource = hasHeaders ? isFlat ? paramsExpr : Member(paramsExpr, "headers") : null,
            HeadersSourceMayBeUndefined = hasHeaders && !isFlat,
            BodySource = hasBody ? BodyPropertyAccess(operation.RequestBody!, paramsExpr) : null,
            BodySourceMayBeUndefined = hasBody && operation.RequestBody?.IsRequired == false
        };
    }

    private static ExpressionNode BodyPropertyAccess(IrRequestBody requestBody, ExpressionNode source) =>
        Member(source, requestBody.IsForm ? "form" : "body");

    private ExpressionNode BuildParameterObjectSchema(IReadOnlyList<IrParameter> parameters)
    {
        var properties = parameters
            .Select(parameter => new PropertyAssignmentNode(
                new IdentifierNode(parameter.Name),
                BuildParameterSchema(parameter)))
            .ToImmutableList();

        return _validationLibrary.Kind switch
        {
            EndpointValidationKind.Zod => ZodCall("object", new ObjectLiteralExpressionNode(properties, preferMultilineFormat: true)),
            EndpointValidationKind.Valibot => Call(Member(Id("v"), "object"), new ObjectLiteralExpressionNode(properties, preferMultilineFormat: true)),
            EndpointValidationKind.TypeBox => Call(Member(Id("Type"), "Object"), new ObjectLiteralExpressionNode(properties, preferMultilineFormat: true)),
            _ => EmptyObject()
        };
    }

    private ExpressionNode BuildParameterSchema(IrParameter parameter)
    {
        var schema = _validationLibrary.Kind switch
        {
            EndpointValidationKind.Zod => BuildZodSchemaFromIrType(parameter.Type),
            EndpointValidationKind.Valibot => _valibotAstBuilder.BuildSchemaFromIrType(parameter.Type),
            EndpointValidationKind.TypeBox => _typeBoxAstBuilder.BuildSchemaFromIrType(parameter.Type),
            _ => EmptyObject()
        };

        if (parameter.IsRequired)
            return schema;

        return _validationLibrary.Kind switch
        {
            EndpointValidationKind.Zod => Call(Member(schema, "optional")),
            EndpointValidationKind.Valibot => Call(Member(Id("v"), "optional"), schema),
            EndpointValidationKind.TypeBox => Call(Member(Id("Type"), "Optional"), schema),
            _ => schema
        };
    }

    private ExpressionNode BuildZodSchemaFromIrType(IrType type)
    {
        return type switch
        {
            IrPrimitiveType primitive => BuildZodPrimitive(primitive.PrimitiveKind),
            IrModelReference reference => Id($"{reference.ModelName}Schema"),
            IrArrayType array => ZodCall("array", BuildZodSchemaFromIrType(array.ElementType)),
            IrDictionaryType dictionary => ZodCall("record", BuildZodSchemaFromIrType(dictionary.ValueType)),
            IrNullableType nullable => Call(Member(BuildZodSchemaFromIrType(nullable.InnerType), "nullable")),
            IrOptionalType optional => Call(Member(BuildZodSchemaFromIrType(optional.InnerType), "optional")),
            IrUnionType union => ZodCall("union", new ArrayLiteralExpressionNode(union.Types.Select(BuildZodSchemaFromIrType).ToImmutableList())),
            _ => ZodCall("unknown")
        };
    }

    private ExpressionNode BuildZodPrimitive(IrPrimitiveKind kind) =>
        kind switch
        {
            IrPrimitiveKind.String => ZodCall("string"),
            IrPrimitiveKind.Number => ZodCall("number"),
            IrPrimitiveKind.Integer or IrPrimitiveKind.Long => Call(Member(ZodCall("number"), "int")),
            IrPrimitiveKind.Boolean => ZodCall("boolean"),
            IrPrimitiveKind.Uuid => _uuidFormat switch
            {
                TypeScriptOptionIds.UuidFormat.String => ZodCall("string"),
                TypeScriptOptionIds.UuidFormat.Regex => Call(Member(ZodCall("string"), "regex"), Id(TypeScriptOptionIds.UuidFormat.RegexConstName)),
                _ => Call(Member(ZodCall("string"), "uuid")),
            },
            IrPrimitiveKind.Any => ZodCall("unknown"),
            IrPrimitiveKind.Void => ZodCall("void"),
            _ => ZodCall("unknown")
        };

    private ExpressionNode BuildParseCall(string schemaName, ExpressionNode input) =>
        _validationLibrary.Kind switch
        {
            EndpointValidationKind.Zod => Call(Member(Id(schemaName), "parse"), input),
            EndpointValidationKind.Valibot => Call(Member(Id("v"), "parse"), Id(schemaName), input),
            EndpointValidationKind.TypeBox => Call(Member(Id("Value"), "Parse"), Id(schemaName), input),
            _ => input
        };

    private IEnumerable<string> ExtractModelSchemaNames(IrOperation operation)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);

        if (operation.RequestBody?.Type is IrModelReference bodyModelRef)
            names.Add($"{bodyModelRef.ModelName}Schema");

        foreach (var modelRef in operation.Responses.Select(response => response.Type).OfType<IrModelReference>())
            names.Add($"{modelRef.ModelName}Schema");

        return names;
    }

    private string BuildSchemaImportPath(string modelName, FileOrganization fileOrganization)
    {
        var schemasPath = fileOrganization.SchemasPath ?? "schemas";
        var modelFileName = _namingConverter.ToCamelCase(modelName);
        return fileOrganization.SeparateModels
            ? $"../../{schemasPath}/{modelFileName}/{modelFileName}.schema"
            : $"../../{schemasPath}/{modelFileName}.schema";
    }

    private void AddUuidFormatImport(ImmutableList<ImportNode>.Builder imports, IrOperation operation, FileOrganization fileOrganization)
    {
        if (_uuidFormat != TypeScriptOptionIds.UuidFormat.Regex || !UuidFormatSupport.OperationParamsUseUuid(operation))
            return;

        var schemasPath = fileOrganization.SchemasPath ?? "schemas";
        var symbol = _validationLibrary.Kind == EndpointValidationKind.TypeBox
            ? TypeScriptOptionIds.UuidFormat.PatternConstName
            : TypeScriptOptionIds.UuidFormat.RegexConstName;
        imports.Add(new ImportNode(
            items: ImmutableList.Create(new IdentifierNode(symbol)),
            from: $"../../{schemasPath}/{TypeScriptOptionIds.UuidFormat.ConstFileName}",
            kind: ImportKind.Named));
    }

    private static ExpressionNode BuildParameterAccess(ExpressionNode source, string name, bool sourceMayBeUndefined) =>
        source is IdentifierExpressionNode { Identifier.Name: "undefined" }
            ? source
            : new MemberAccessExpressionNode(source, new IdentifierNode(name), useOptionalChaining: sourceMayBeUndefined);

    private static CommentNode? BuildDocumentation(IrOperation operation)
    {
        var doc = operation.Description ?? operation.Summary;
        return string.IsNullOrWhiteSpace(doc) ? null : new CommentNode(doc, CommentKind.JSDoc);
    }

    private static PropertyAssignmentNode SpreadProperty(ExpressionNode value) =>
        new(new IdentifierNode("_"), value, isSpread: true);

    private static ObjectLiteralExpressionNode Object(params (string Name, ExpressionNode Value)[] properties) =>
        new(properties
            .Select(property => property.Name == "_"
                ? SpreadProperty(property.Value)
                : new PropertyAssignmentNode(new IdentifierNode(property.Name), property.Value))
            .ToImmutableList(),
            preferMultilineFormat: true);

    private static ObjectLiteralExpressionNode EmptyObject() =>
        new(ImmutableList<PropertyAssignmentNode>.Empty);

    private static CallExpressionNode ZodCall(string methodName, params ExpressionNode[] arguments) =>
        Call(Member(Id("z"), methodName), arguments);

    private static CallExpressionNode Call(ExpressionNode target, params ExpressionNode[] arguments) =>
        new(target, arguments.ToImmutableList());

    private static IdentifierExpressionNode Id(string name) =>
        new(new IdentifierNode(name));

    private static MemberAccessExpressionNode Member(ExpressionNode source, string name, bool optional = false) =>
        new(source, new IdentifierNode(name), useOptionalChaining: optional);

    private static SimpleTypeNode Simple(string name, bool isPrimitive = false) =>
        new(new IdentifierNode(name), isPrimitive);

    private static LiteralExpressionNode String(string value) =>
        new(value, LiteralKind.String);

    private static LiteralExpressionNode Number(int value) =>
        new(value, LiteralKind.Number);

    private static LiteralExpressionNode Bool(bool value) =>
        new(value, LiteralKind.Boolean);

    private sealed class ParameterSources
    {
        public ExpressionNode? RouteSource { get; set; }
        public ExpressionNode? QuerySource { get; set; }
        public bool QuerySourceMayBeUndefined { get; set; }
        public ExpressionNode? HeadersSource { get; set; }
        public bool HeadersSourceMayBeUndefined { get; set; }
        public ExpressionNode? BodySource { get; set; }
        public bool BodySourceMayBeUndefined { get; set; }
    }

    private enum EndpointValidationKind
    {
        None,
        Zod,
        Valibot,
        TypeBox
    }

    private sealed record EndpointValidationLibrary(EndpointValidationKind Kind, string ZodImportPath)
    {
        public bool IsEnabled => Kind != EndpointValidationKind.None;

        public static EndpointValidationLibrary From(TypeScriptGenerationOptions? options) =>
            options?.ValidationLibrary switch
            {
                TypeScriptOptionIds.Validation.Zod4 => new EndpointValidationLibrary(EndpointValidationKind.Zod, "zod/v4"),
                TypeScriptOptionIds.Validation.Zod3 => new EndpointValidationLibrary(EndpointValidationKind.Zod, "zod"),
                TypeScriptOptionIds.Validation.Valibot => new EndpointValidationLibrary(EndpointValidationKind.Valibot, "zod"),
                TypeScriptOptionIds.Validation.TypeBox => new EndpointValidationLibrary(EndpointValidationKind.TypeBox, "zod"),
                _ => new EndpointValidationLibrary(EndpointValidationKind.None, "zod")
            };
    }
}
