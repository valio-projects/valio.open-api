using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Valibot.Ast.Builders;

/// <summary>
/// Builds Valibot schema source files as TypeScript AST.
/// </summary>
public sealed class ValibotSchemaSourceFileBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly ValibotAstBuilder _builder = new();

    public ValibotSchemaSourceFileBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    public SourceFileNode BuildModelSchemaSourceFile(IrModel model, bool separateModels)
    {
        ArgumentNullException.ThrowIfNull(model);

        var schemaName = GetSchemaName(model.Name);
        var isRecursive = IsSelfRecursive(model);
        // Для рекурсивных схем TS не может вывести тип из v.lazy(...) без явной аннотации.
        // Декларируем `: v.GenericSchema<{ModelName}>` и подтягиваем тип модели.
        var schemaType = isRecursive
            ? (TypeNode)new RawTypeNode($"v.GenericSchema<{model.Name}>")
            : null;

        return new SourceFileNode(
            imports: BuildImports(model, separateModels, includeOwnModelType: isRecursive),
            declarations: ImmutableList<DeclarationNode>.Empty
                .Add(new VariableDeclarationNode(
                    name: new IdentifierNode(schemaName),
                    declarationKind: VariableDeclarationKind.Const,
                    type: schemaType,
                    initializer: _builder.BuildSchemaFromIrModel(model),
                    documentation: BuildDocumentation(model.Description),
                    isExported: true))
                .Add(BuildInferOutputTypeAlias(model.Name, schemaName)));
    }

    private static bool IsSelfRecursive(IrModel model)
    {
        var seen = new HashSet<IrType>();
        foreach (var property in model.Properties)
        {
            if (ReferencesSelf(property.Type, model.Name, seen))
                return true;
        }
        if (model.Composition != null)
        {
            foreach (var type in model.Composition.Types)
            {
                if (ReferencesSelf(type, model.Name, seen))
                    return true;
            }
        }
        return false;
    }

    private static bool ReferencesSelf(IrType type, string ownName, HashSet<IrType> seen)
    {
        if (!seen.Add(type)) return false;
        switch (type)
        {
            case IrModelReference m:
                return string.Equals(m.ModelName, ownName, StringComparison.OrdinalIgnoreCase);
            case IrArrayType a:
                return ReferencesSelf(a.ElementType, ownName, seen);
            case IrDictionaryType d:
                return ReferencesSelf(d.KeyType, ownName, seen) || ReferencesSelf(d.ValueType, ownName, seen);
            case IrNullableType n:
                return ReferencesSelf(n.InnerType, ownName, seen);
            case IrOptionalType o:
                return ReferencesSelf(o.InnerType, ownName, seen);
            case IrUnionType u:
                return u.Types.Any(t => ReferencesSelf(t, ownName, seen));
            case IrIntersectionType i:
                return i.Types.Any(t => ReferencesSelf(t, ownName, seen));
            default:
                return false;
        }
    }

    public SourceFileNode BuildEnumSchemaSourceFile(IrEnum enumModel)
    {
        ArgumentNullException.ThrowIfNull(enumModel);

        var valuesName = $"{enumModel.Name}Values";
        var schemaName = GetSchemaName(enumModel.Name);
        var values = enumModel.Values
            .Select(value => BuildEnumValueLiteral(value.Value, enumModel.BaseType))
            .ToImmutableList<ExpressionNode>();

        return new SourceFileNode(
            imports: BuildValibotImport(),
            declarations: ImmutableList<DeclarationNode>.Empty
                .Add(new VariableDeclarationNode(
                    name: new IdentifierNode(valuesName),
                    declarationKind: VariableDeclarationKind.Const,
                    initializer: new ArrayLiteralExpressionNode(values, preferMultilineFormat: true),
                    isExported: true,
                    asConstAssertion: true))
                .Add(new VariableDeclarationNode(
                    name: new IdentifierNode(schemaName),
                    declarationKind: VariableDeclarationKind.Const,
                    initializer: _builder.BuildEnumSchema(enumModel),
                    documentation: BuildDocumentation(enumModel.Description),
                    isExported: true))
                .Add(BuildInferOutputTypeAlias(enumModel.Name, schemaName)));
    }

    private static string GetSchemaName(string name) => $"{name}Schema";

    private static IReadOnlyList<ImportNode> BuildValibotImport()
    {
        return ImmutableList<ImportNode>.Empty.Add(new ImportNode(
            items: ImmutableList.Create(new IdentifierNode("v")),
            from: "valibot",
            kind: ImportKind.Namespace));
    }

    private IReadOnlyList<ImportNode> BuildImports(IrModel model, bool separateModels, bool includeOwnModelType = false)
    {
        var imports = ImmutableList.CreateBuilder<ImportNode>();
        imports.AddRange(BuildValibotImport());

        // Для рекурсивных схем — нужен тип самой модели (для аннотации v.GenericSchema<T>).
        if (includeOwnModelType)
        {
            var ownFileName = _namingConverter.ToCamelCase(model.Name);
            var ownPath = separateModels
                ? $"../../{ownFileName}/{ownFileName}"
                : $"../{ownFileName}";
            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(model.Name)),
                from: ownPath,
                kind: ImportKind.Named,
                isTypeOnly: true));
        }

        var dependencies = ExtractDependencies(model)
            .Where(dep => !dep.Equals(model.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(dep => dep, StringComparer.Ordinal)
            .ToList();

        foreach (var dependency in dependencies)
        {
            var depFileName = _namingConverter.ToCamelCase(dependency);
            var importPath = separateModels
                ? $"../{depFileName}/{depFileName}.schema"
                : $"./{depFileName}.schema";

            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(GetSchemaName(dependency))),
                from: importPath,
                kind: ImportKind.Named));
        }

        return imports.ToImmutable();
    }

    private static TypeAliasDeclarationNode BuildInferOutputTypeAlias(string name, string schemaName)
    {
        return new TypeAliasDeclarationNode(
            name: new IdentifierNode($"{name}SchemaOutput"),
            type: new RawTypeNode($"v.InferOutput<typeof {schemaName}>"));
    }

    private static CommentNode? BuildDocumentation(string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? null
            : new CommentNode(description, CommentKind.JSDoc);
    }

    private static LiteralExpressionNode BuildEnumValueLiteral(object? value, IrEnumBaseType baseType)
    {
        if (baseType == IrEnumBaseType.Integer)
            return new LiteralExpressionNode(value ?? 0, LiteralKind.Number);

        return new LiteralExpressionNode(value?.ToString() ?? string.Empty, LiteralKind.String);
    }

    private IReadOnlyList<string> ExtractDependencies(IrModel model)
    {
        var dependencies = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in model.Properties)
            ExtractDependenciesFromIrType(property.Type, dependencies);

        if (model.Composition != null)
        {
            foreach (var type in model.Composition.Types)
                ExtractDependenciesFromIrType(type, dependencies);
        }

        return dependencies.ToList();
    }

    private void ExtractDependenciesFromIrType(IrType irType, HashSet<string> dependencies)
    {
        switch (irType)
        {
            case IrModelReference modelReference:
                dependencies.Add(modelReference.ModelName);
                break;
            case IrArrayType arrayType:
                ExtractDependenciesFromIrType(arrayType.ElementType, dependencies);
                break;
            case IrDictionaryType dictionaryType:
                ExtractDependenciesFromIrType(dictionaryType.KeyType, dependencies);
                ExtractDependenciesFromIrType(dictionaryType.ValueType, dependencies);
                break;
            case IrUnionType unionType:
                foreach (var type in unionType.Types)
                    ExtractDependenciesFromIrType(type, dependencies);
                break;
            case IrIntersectionType intersectionType:
                foreach (var type in intersectionType.Types)
                    ExtractDependenciesFromIrType(type, dependencies);
                break;
            case IrNullableType nullableType:
                ExtractDependenciesFromIrType(nullableType.InnerType, dependencies);
                break;
            case IrOptionalType optionalType:
                ExtractDependenciesFromIrType(optionalType.InnerType, dependencies);
                break;
        }
    }
}
