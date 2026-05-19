using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.Ast.Nodes;

namespace ValIO.Generators.TypeScript.TypeBox.Ast.Builders;

/// <summary>
/// Builds TypeBox schema source files as TypeScript AST.
/// </summary>
public sealed class TypeBoxSchemaSourceFileBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeBoxAstBuilder _builder = new();

    public TypeBoxSchemaSourceFileBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public SourceFileNode BuildModelSchemaSourceFile(IrModel model, string? currentFileName = null, string? schemasPath = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var schemaName = GetSchemaName(model.Name);
        return new SourceFileNode(
            imports: BuildImports(model, currentFileName, schemasPath),
            declarations: ImmutableList<DeclarationNode>.Empty
                .Add(new VariableDeclarationNode(
                    name: new IdentifierNode(schemaName),
                    declarationKind: VariableDeclarationKind.Const,
                    initializer: _builder.BuildSchemaFromIrModel(model),
                    documentation: BuildDocumentation(model.Description),
                    isExported: true))
                .Add(BuildStaticTypeAlias(model.Name, schemaName)));
    }

    public SourceFileNode BuildEnumSchemaSourceFile(IrEnum irEnum)
    {
        ArgumentNullException.ThrowIfNull(irEnum);

        var schemaName = GetSchemaName(irEnum.Name);
        return new SourceFileNode(
            imports: BuildTypeBoxImports(),
            declarations: ImmutableList<DeclarationNode>.Empty
                .Add(new VariableDeclarationNode(
                    name: new IdentifierNode(schemaName),
                    declarationKind: VariableDeclarationKind.Const,
                    initializer: _builder.BuildEnumSchema(irEnum),
                    documentation: BuildDocumentation(irEnum.Description),
                    isExported: true))
                .Add(BuildStaticTypeAlias(irEnum.Name, schemaName)));
    }

    private static string GetSchemaName(string name) => $"{name}Schema";

    private IReadOnlyList<ImportNode> BuildImports(IrModel model, string? currentFileName, string? schemasPath)
    {
        var imports = ImmutableList.CreateBuilder<ImportNode>();
        imports.AddRange(BuildTypeBoxImports());
        var dependencies = ExtractDependencies(model);

        foreach (var dependency in dependencies.OrderBy(name => name, StringComparer.Ordinal))
        {
            imports.Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode(GetSchemaName(dependency))),
                from: CalculateImportPath(currentFileName, dependency, schemasPath),
                kind: ImportKind.Named));
        }

        return imports.ToImmutable();
    }

    private static IReadOnlyList<ImportNode> BuildTypeBoxImports()
    {
        return ImmutableList<ImportNode>.Empty
            .Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode("Type")),
                from: "@sinclair/typebox",
                kind: ImportKind.Named))
            .Add(new ImportNode(
                items: ImmutableList.Create(new IdentifierNode("Static")),
                from: "@sinclair/typebox",
                kind: ImportKind.Named,
                isTypeOnly: true));
    }

    private static TypeAliasDeclarationNode BuildStaticTypeAlias(string name, string schemaName)
    {
        return new TypeAliasDeclarationNode(
            name: new IdentifierNode($"{name}SchemaOutput"),
            type: new RawTypeNode($"Static<typeof {schemaName}>"));
    }

    private static CommentNode? BuildDocumentation(string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? null
            : new CommentNode(description, CommentKind.JSDoc);
    }

    private static IReadOnlyList<string> ExtractDependencies(IrModel model)
    {
        var dependencies = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in model.Properties)
            ExtractDependenciesFromType(property.Type, model.Name, dependencies);

        if (model.Composition != null)
        {
            foreach (var type in model.Composition.Types)
                ExtractDependenciesFromType(type, model.Name, dependencies);
        }

        return dependencies.ToList();
    }

    private static void ExtractDependenciesFromType(IrType type, string currentModelName, HashSet<string> dependencies)
    {
        switch (type)
        {
            case IrModelReference modelRef when !modelRef.ModelName.Equals(currentModelName, StringComparison.Ordinal):
                dependencies.Add(modelRef.ModelName);
                break;
            case IrArrayType array:
                ExtractDependenciesFromType(array.ElementType, currentModelName, dependencies);
                break;
            case IrDictionaryType dictionary:
                ExtractDependenciesFromType(dictionary.KeyType, currentModelName, dependencies);
                ExtractDependenciesFromType(dictionary.ValueType, currentModelName, dependencies);
                break;
            case IrNullableType nullable:
                ExtractDependenciesFromType(nullable.InnerType, currentModelName, dependencies);
                break;
            case IrOptionalType optional:
                ExtractDependenciesFromType(optional.InnerType, currentModelName, dependencies);
                break;
            case IrUnionType union:
                foreach (var child in union.Types)
                    ExtractDependenciesFromType(child, currentModelName, dependencies);
                break;
            case IrIntersectionType intersection:
                foreach (var child in intersection.Types)
                    ExtractDependenciesFromType(child, currentModelName, dependencies);
                break;
        }
    }

    private string CalculateImportPath(string? currentFileName, string dependencyFileName, string? schemasPath)
    {
        if (!string.IsNullOrEmpty(schemasPath) && !string.IsNullOrEmpty(currentFileName))
        {
            var normalizedSchemasPath = NormalizePath(schemasPath);
            var currentBasePath = $"{normalizedSchemasPath}/{currentFileName}";
            var dependencyDir = $"{normalizedSchemasPath}/{dependencyFileName}";
            return NormalizePath(CalculateRelativePath(currentBasePath, dependencyDir, $"{dependencyFileName}.schema"));
        }

        return $"./{dependencyFileName}.schema";
    }

    private static string CalculateRelativePath(string fromPath, string toPath, string fileName)
    {
        fromPath = NormalizePath(fromPath);
        toPath = NormalizePath(toPath);

        if (fromPath.Equals(toPath, StringComparison.OrdinalIgnoreCase))
            return $"./{fileName}";

        var fromParts = fromPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var toParts = toPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        var commonLength = 0;
        var minLength = Math.Min(fromParts.Length, toParts.Length);
        for (var i = 0; i < minLength; i++)
        {
            if (!fromParts[i].Equals(toParts[i], StringComparison.OrdinalIgnoreCase))
                break;
            commonLength++;
        }

        var upLevels = fromParts.Length - commonLength;
        var downPath = string.Join("/", toParts.Skip(commonLength));
        var relativePath = string.Join("/", Enumerable.Repeat("..", upLevels));

        if (!string.IsNullOrEmpty(downPath))
            relativePath = string.IsNullOrEmpty(relativePath) ? downPath : $"{relativePath}/{downPath}";

        return $"{relativePath}/{fileName}";
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Replace("//", "/").TrimEnd('/');
}
