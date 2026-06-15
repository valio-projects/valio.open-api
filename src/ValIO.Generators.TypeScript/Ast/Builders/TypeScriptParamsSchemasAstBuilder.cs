using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Generators.TypeScript.Ast.JsDoc;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Validation;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Строит <c>{operation}.params.schema.ts</c> — именованные схемы валидации route/query параметров,
/// которые иначе пришлось бы строить inline в каждом клиенте. Каждая объявляется один раз как
/// <c>export const {Op}RouteParamsSchema = ...;</c> / <c>{Op}QueryParamsSchema = ...;</c> и импортируется
/// всеми транспортами. Библиотека (zod/valibot/typebox) выбирается стратегией — файл от неё не зависит.
/// </summary>
public sealed class TypeScriptParamsSchemasAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly IClientValidationStrategy _validation;
    private readonly string _uuidFormat;

    internal TypeScriptParamsSchemasAstBuilder(INamingConverter namingConverter, IClientValidationStrategy validation, string? uuidFormat = null)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _validation = validation ?? throw new ArgumentNullException(nameof(validation));
        _uuidFormat = TypeScriptOptionIds.UuidFormat.Resolve(uuidFormat);
    }

    /// <summary>Имя константы схемы route-параметров: <c>{Op}RouteParamsSchema</c>.</summary>
    public string RouteParamsSchemaName(string operationName)
        => $"{_namingConverter.ToPascalCase(operationName)}RouteParamsSchema";

    /// <summary>Имя константы схемы query-параметров: <c>{Op}QueryParamsSchema</c>.</summary>
    public string QueryParamsSchemaName(string operationName)
        => $"{_namingConverter.ToPascalCase(operationName)}QueryParamsSchema";

    /// <summary>
    /// Строит файл со схемами параметров. Возвращает <c>null</c>, если валидация выключена или у
    /// операции нет route/query параметров.
    /// </summary>
    public SourceFileNode? BuildParamsSchemasSourceFile(IrOperation operation, FileOrganization? fileOrganization)
    {
        if (!_validation.IsEnabled)
            return null;

        var hasRoute = operation.PathParameters.Count > 0;
        var hasQuery = operation.QueryParameters.Count > 0;
        if (!hasRoute && !hasQuery)
            return null;

        var declarations = new List<DeclarationNode>();
        if (hasRoute)
            declarations.Add(BuildParamsSchemaDeclaration(
                RouteParamsSchemaName(operation.MethodName), operation.PathParameters, "Route (path) parameter validation schema."));
        if (hasQuery)
            declarations.Add(BuildParamsSchemaDeclaration(
                QueryParamsSchemaName(operation.MethodName), operation.QueryParameters, "Query parameter validation schema."));

        var paramTypes = operation.PathParameters.Concat(operation.QueryParameters).Select(p => p.Type).ToList();
        var imports = BuildImports(operation, paramTypes, fileOrganization);

        var fileBase = _namingConverter.ToCamelCase(operation.MethodName);
        return new SourceFileNode(
            imports: imports,
            declarations: declarations,
            fileDocumentation: TypeScriptDoc.FileHeader(
                $"{fileBase}.params.schema.ts",
                $"{fileBase}.params.schema",
                $"Route/query parameter validation schemas for {operation.MethodName}."));
    }

    private DeclarationNode BuildParamsSchemaDeclaration(string schemaName, IReadOnlyList<IrParameter> parameters, string summary)
        => new VariableDeclarationNode(
            name: new IdentifierNode(schemaName),
            declarationKind: VariableDeclarationKind.Const,
            initializer: _validation.BuildParamsSchema(parameters),
            documentation: new CommentNode(summary, CommentKind.JSDoc),
            isExported: true);

    private IReadOnlyList<ImportNode> BuildImports(IrOperation operation, IReadOnlyList<IrType> paramTypes, FileOrganization? fileOrganization)
    {
        var imports = new List<ImportNode>();
        imports.AddRange(_validation.SchemaDefinitionImports(paramTypes));

        var schemasPath = fileOrganization?.SchemasPath ?? "schemas";
        var separateModels = fileOrganization?.SeparateModels ?? true;

        // Shared UUID-константа (uuidFormat = regex): схема параметра рендерится как `.regex(UUID_REGEX)` /
        // `pattern: UUID_PATTERN` и обязана импортировать константу из общего файла schemas/formats.ts.
        if (_validation.UuidConstName != null
            && _uuidFormat == TypeScriptOptionIds.UuidFormat.Regex
            && UuidFormatSupport.OperationParamsUseUuid(operation))
        {
            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(_validation.UuidConstName)),
                from: $"../../{schemasPath}/{TypeScriptOptionIds.UuidFormat.ConstFileName}",
                kind: ImportKind.Named));
        }
        var modelNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in paramTypes)
            ValidationTypes.CollectModelSchemaNames(type, modelNames);

        foreach (var modelName in modelNames.OrderBy(n => n, StringComparer.Ordinal))
        {
            var modelFile = _namingConverter.ToCamelCase(modelName);
            var from = separateModels
                ? $"../../{schemasPath}/{modelFile}/{modelFile}.schema"
                : $"../../{schemasPath}/{modelFile}.schema";
            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode($"{modelName}Schema")),
                from: from,
                kind: ImportKind.Named));
        }

        return imports;
    }
}
