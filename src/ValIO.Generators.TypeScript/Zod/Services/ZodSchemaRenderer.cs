using System.Collections.Generic;
using System.Linq;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Core.Ast.Builders;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.JsDoc;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Zod.Ast.Builders;
using ValIO.Generators.TypeScript.Zod.Ast.Nodes;
using ValIO.Generators.TypeScript.Zod.Ast.Renderers;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Validation;

namespace ValIO.Generators.TypeScript.Zod;

/// <summary>
/// Renders Zod validation schemas using the shared IR document and schema AST.
/// </summary>
public class ZodSchemaRenderer : AstBuilderBase
{
    private readonly INamingConverter _namingConverter;
    private readonly Func<IrType, ValIO.Core.Ast.Nodes.TypeNode> _typeBuilder;
    private readonly bool _useBigInt;
    private readonly string _zodImportPath;
    private readonly string _modelsPath;
    private readonly string _uuidFormat;
    /// <summary>Whether generated schemas decode lossless numeric tokens.</summary>
    private readonly bool _exactJsonNumbers;

    /// <summary>Creates a renderer bound to one finalized document context.</summary>
    /// <param name="documentContext">The shared immutable declaration indexes.</param>
    /// <param name="namingConverter">The naming rules for declaration file stems.</param>
    /// <param name="typeBuilder">The conversion from IR types to TypeScript type nodes.</param>
    /// <param name="useBigInt">Whether int64 values use bigint schemas.</param>
    /// <param name="zodVersion">The selected Zod major version.</param>
    /// <param name="modelsPath">The configured model output directory.</param>
    /// <param name="uuidFormat">The selected UUID validation mode.</param>
    /// <param name="exactJsonNumbers">Whether the profile preserves exact JSON numbers.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public ZodSchemaRenderer(
        IrDocumentContext documentContext,
        INamingConverter namingConverter,
        Func<IrType, TypeNode> typeBuilder,
        bool useBigInt = false,
        string zodVersion = "zod3",
        string modelsPath = "models",
        string? uuidFormat = null,
        bool exactJsonNumbers = false)
        : base(documentContext)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeBuilder = typeBuilder ?? throw new ArgumentNullException(nameof(typeBuilder));
        _useBigInt = useBigInt;
        _zodImportPath = zodVersion == "zod4" ? "zod/v4" : "zod";
        _modelsPath = string.IsNullOrWhiteSpace(modelsPath) ? "models" : modelsPath;
        _uuidFormat = TypeScriptOptionIds.UuidFormat.Resolve(uuidFormat);
        _exactJsonNumbers = exactJsonNumbers;
    }

    /// <summary>Gets whether this profile validates UUIDs with the shared regular expression.</summary>
    /// <value>Whether UUID schemas refer to the generated formats module.</value>
    private bool UsesUuidRegex => _uuidFormat == TypeScriptOptionIds.UuidFormat.Regex;

    /// <summary>Renders a Zod schema for a model.</summary>
    /// <param name="model">The model whose schema is rendered.</param>
    /// <param name="importPath">An optional explicit model import base path.</param>
    /// <param name="currentFileName">The current file stem used to calculate dependency paths.</param>
    /// <param name="schemasPath">The schema directory used to calculate dependency paths.</param>
    /// <returns>The TypeScript source for the model schema.</returns>
    public string RenderFromIr(IrModel model, string? importPath = null, string? currentFileName = null, string? schemasPath = null) =>
        new TypeScriptAstRenderer([new ZodNodeRenderer()]).Render(BuildModel(model, importPath, currentFileName, schemasPath));

    /// <summary>Builds the type annotation, initializer and imports for a model schema module.</summary>
    /// <param name="model">The model whose schema is generated.</param>
    /// <param name="importPath">An optional explicit model import base path.</param>
    /// <param name="currentFileName">The current file stem.</param>
    /// <param name="schemasPath">The schema output directory.</param>
    /// <returns>The complete schema module tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    internal SourceFileNode BuildModel(IrModel model, string? importPath, string? currentFileName, string? schemasPath)
    {
        ArgumentNullException.ThrowIfNull(model);
        var imports = ZodImports();
        var modelFileName = _namingConverter.ToCamelCase(model.OriginalName);
        var modelImport = !string.IsNullOrEmpty(importPath)
            ? $"{NormalizePath(importPath)}/{modelFileName}/{modelFileName}"
            : BuildModelImportPathFromSchema(currentFileName, schemasPath, modelFileName);
        imports.Add(new ImportNode([new(model.Name)], modelImport, isTypeOnly: true));

        var builder = new ZodAstBuilder(DocumentContext, _namingConverter, _typeBuilder, _useBigInt, _uuidFormat, _exactJsonNumbers);
        var schema = builder.BuildZodSchemaFromIrModel(model);
        var constraintsName = model.Name + "Constraints";
        if (TypeScriptIdentifierUsage.References(schema, constraintsName))
            imports.Add(new ImportNode([new(constraintsName)], modelImport));
        if (UsesUuidRegex && UuidFormatSupport.ModelUsesUuid(model))
        {
            var formats = TypeScriptOptionIds.UuidFormat.ConstFileName;
            imports.Add(new ImportNode([new(TypeScriptOptionIds.UuidFormat.RegexConstName)], $"../{formats}"));
        }
        if (_exactJsonNumbers && ((model.ScalarType is { } scalar && TypeScriptExactJson.UsesNumberCodec(scalar))
            || model.Properties.Any(property => TypeScriptExactJson.UsesNumberCodec(property.Type))
            || model.Composition?.Types.Any(TypeScriptExactJson.UsesNumberCodec) == true))
        {
            imports.Add(TypeScriptExactJson.Import());
        }

        foreach (var dependency in ExtractDependencies(model).OrderBy(name => name))
        {
            var relative = CalculateImportPath(currentFileName, SchemaNameToFileName(dependency), schemasPath, importPath: null);
            imports.Add(new ImportNode([new(dependency)], relative));
        }

        return BuildFile(model.Name, modelFileName, model.Description,
            $"Zod validation schema for {model.Name}.", imports, schema);
    }

    /// <summary>Renders an enum schema with a checked output type and unknown external input.</summary>
    /// <param name="enumModel">The enum whose schema is rendered.</param>
    /// <param name="importPath">An optional explicit enum import base path.</param>
    /// <param name="currentFileName">The current file stem used to calculate dependency paths.</param>
    /// <param name="schemasPath">The schema directory used to calculate dependency paths.</param>
    /// <returns>The TypeScript source for the enum schema.</returns>
    public string RenderEnumFromIr(IrEnum enumModel, string? importPath = null, string? currentFileName = null, string? schemasPath = null) =>
        new TypeScriptAstRenderer([new ZodNodeRenderer()]).Render(BuildEnum(enumModel, importPath, currentFileName, schemasPath));

    /// <summary>Builds one enum schema module using the model's exported values for string enums.</summary>
    /// <param name="enumModel">The enum whose schema is generated.</param>
    /// <param name="importPath">An optional explicit enum import base path.</param>
    /// <param name="currentFileName">The current file stem.</param>
    /// <param name="schemasPath">The schema output directory.</param>
    /// <returns>The complete enum schema module tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enumModel"/> is null.</exception>
    internal SourceFileNode BuildEnum(IrEnum enumModel, string? importPath, string? currentFileName, string? schemasPath)
    {
        ArgumentNullException.ThrowIfNull(enumModel);
        var imports = ZodImports();
        var typeName = enumModel.Name;
        var enumFileName = _namingConverter.ToCamelCase(enumModel.OriginalName);
        var enumImport = !string.IsNullOrEmpty(importPath)
            ? $"{NormalizePath(importPath)}/{enumFileName}/{enumFileName}"
            : BuildModelImportPathFromSchema(currentFileName, schemasPath, enumFileName);
        imports.Add(new ImportNode([new(typeName)], enumImport, isTypeOnly: true));

        ExpressionNode schema;
        if (enumModel.BaseType == IrEnumBaseType.String)
        {
            var values = new IdentifierNode(typeName + "Values");
            imports.Add(new ImportNode([values], enumImport));
            var factory = new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("z")), new("enum"));
            schema = new CallExpressionNode(factory, [new IdentifierExpressionNode(values)]);
        }
        else
        {
            var builder = new ZodAstBuilder(DocumentContext, _namingConverter, _typeBuilder, _useBigInt, _uuidFormat);
            var zodEnum = builder.BuildZodEnum(enumModel);
            if (_exactJsonNumbers)
            {
                imports.Add(TypeScriptExactJson.Import());
                schema = new ZodExpressionSchemaNode(TypeScriptExactJson.OrdinaryNumberSchema(zodEnum, integer: true));
            }
            else
            {
                schema = zodEnum;
            }
        }

        return BuildFile(typeName, enumFileName, enumModel.Description,
            $"Zod validation schema for the {typeName} enum.", imports, schema);
    }

    /// <summary>Imports the Zod runtime separately from its type declarations.</summary>
    /// <returns>The initial imports for a schema module.</returns>
    private List<ImportNode> ZodImports() =>
        [new([new("z")], _zodImportPath), new([new("ZodSchema")], _zodImportPath, isTypeOnly: true)];

    /// <summary>Builds the module with a named, typed exported schema.</summary>
    /// <param name="typeName">The declared schema output type.</param>
    /// <param name="fileName">The module file stem.</param>
    /// <param name="description">The original schema description, if any.</param>
    /// <param name="fileDescription">The generated file header description.</param>
    /// <param name="imports">The module dependencies.</param>
    /// <param name="schema">The schema initializer expression.</param>
    /// <returns>The source file tree handed to the TypeScript printer.</returns>
    private SourceFileNode BuildFile(string typeName, string fileName, string? description, string fileDescription,
        IReadOnlyList<ImportNode> imports, ExpressionNode schema)
    {
        var type = ZodSchemaTypes.Build(new SimpleTypeNode(new IdentifierNode(typeName)), _zodImportPath);
        var declaration = new VariableDeclarationNode(new(typeName + "Schema"), type: type, initializer: schema,
            documentation: string.IsNullOrEmpty(description) ? null : new CommentNode(description, CommentKind.JSDoc),
            isExported: true);
        var header = TypeScriptDoc.FileHeader($"{fileName}.schema.ts", $"{_modelsPath}/{fileName}", fileDescription);
        return new SourceFileNode(imports: imports, declarations: [declaration], fileDocumentation: header);
    }

    private IReadOnlyList<string> ExtractDependencies(IrModel model)
    {
        var dependencies = new HashSet<string>();
        
        foreach (var property in model.Properties)
        {
            ExtractDependenciesFromIrType(property.Type, dependencies, model.Name);
        }
        
        if (model.Composition != null)
        {
            foreach (var type in model.Composition.Types)
            {
                ExtractDependenciesFromIrType(type, dependencies, model.Name);
            }
        }
        
        return dependencies.ToList();
    }
    
    /// <summary>
    /// Рекурсивно извлекает зависимости из IR типа
    /// </summary>
    private void ExtractDependenciesFromIrType(
        ValIO.Core.IR.Types.IrType irType, 
        HashSet<string> dependencies, 
        string currentModelName)
    {
        switch (irType)
        {
            case ValIO.Core.IR.Types.IrModelReference modelRef:
                if (!modelRef.ModelName.Equals(currentModelName, StringComparison.OrdinalIgnoreCase))
                {
                    var schemaName = $"{modelRef.ModelName}Schema";
                    dependencies.Add(schemaName);
                }
                break;
            case ValIO.Core.IR.Types.IrArrayType arrayType:
                ExtractDependenciesFromIrType(arrayType.ElementType, dependencies, currentModelName);
                break;
            case ValIO.Core.IR.Types.IrDictionaryType dictType:
                ExtractDependenciesFromIrType(dictType.KeyType, dependencies, currentModelName);
                ExtractDependenciesFromIrType(dictType.ValueType, dependencies, currentModelName);
                break;
            case ValIO.Core.IR.Types.IrUnionType unionType:
                foreach (var t in unionType.Types)
                    ExtractDependenciesFromIrType(t, dependencies, currentModelName);
                break;
            case ValIO.Core.IR.Types.IrIntersectionType intersectionType:
                foreach (var t in intersectionType.Types)
                    ExtractDependenciesFromIrType(t, dependencies, currentModelName);
                break;
            case ValIO.Core.IR.Types.IrNullableType nullableType:
                ExtractDependenciesFromIrType(nullableType.InnerType, dependencies, currentModelName);
                break;
            case ValIO.Core.IR.Types.IrOptionalType optionalType:
                ExtractDependenciesFromIrType(optionalType.InnerType, dependencies, currentModelName);
                break;
        }
    }
    
    /// <summary>Resolves a referenced declaration's original file stem independently of build order.</summary>
    /// <param name="schemaName">The generated schema export name.</param>
    /// <returns>The original declaration's camel-case file stem.</returns>
    private string SchemaNameToFileName(string schemaName)
    {
        var typeName = schemaName.EndsWith("Schema", StringComparison.OrdinalIgnoreCase)
            ? schemaName.Substring(0, schemaName.Length - 6)
            : schemaName;

        if (string.IsNullOrEmpty(typeName))
            return schemaName.ToLowerInvariant();

        return _namingConverter.ToCamelCase(OriginalNameMap.GetTypeOriginalName(DocumentContext, typeName));
    }
    
    /// <summary>
    /// Builds the relative import path from a Zod schema file to its model/enum interface file.
    /// </summary>
    /// <remarks>
    /// Model schemas are co-located with the model: the schema lives at
    /// <c>{modelsPath}/{name}/{name}.schema.ts</c> (or <c>{modelsPath}/{name}.schema.ts</c> when
    /// <c>separateModels</c> is false) right next to <c>{name}.ts</c>, so the interface is always a
    /// sibling in the same directory — <c>./{name}</c>.
    /// </remarks>
    /// <param name="currentFileName">The schema's own file-name stem (unused; retained for symmetry).</param>
    /// <param name="schemasPath">The schema base directory (unused; retained for symmetry).</param>
    /// <param name="modelFileName">The model/enum file-name stem.</param>
    /// <returns>A same-directory relative specifier, e.g. <c>./coefficientsInlineModel</c>.</returns>
    private string BuildModelImportPathFromSchema(string? currentFileName, string? schemasPath, string modelFileName)
        => $"./{modelFileName}";

    /// <summary>
    /// Вычисляет относительный путь для импорта схемы.
    /// При структуре models/modelName/ — из currentModel к depModel: ../depModel/depModel.schema
    /// </summary>
    private string CalculateImportPath(string? currentFileName, string dependencyFileName, string? schemasPath, string? importPath)
    {
        if (!string.IsNullOrEmpty(importPath))
        {
            var normalizedPath = NormalizePath(importPath);
            return $"{normalizedPath}/{dependencyFileName}/{dependencyFileName}.schema";
        }
        
        if (!string.IsNullOrEmpty(schemasPath) && !string.IsNullOrEmpty(currentFileName))
        {
            var modelsPath = NormalizePath(schemasPath);
            var currentBasePath = $"{modelsPath}/{currentFileName}";
            var dependencyDir = $"{modelsPath}/{dependencyFileName}";
            var relativePath = CalculateRelativePath(currentBasePath, dependencyDir, $"{dependencyFileName}.schema");
            return NormalizePath(relativePath);
        }
        
        return $"./{dependencyFileName}.schema";
    }
    
    /// <summary>
    /// Вычисляет относительный путь между двумя файлами
    /// </summary>
    private string CalculateRelativePath(string fromPath, string toPath, string fileName)
    {
        fromPath = NormalizePath(fromPath);
        toPath = NormalizePath(toPath);
        
        // Если пути одинаковые, возвращаем просто имя файла
        if (fromPath.Equals(toPath, StringComparison.OrdinalIgnoreCase))
        {
            return $"./{fileName}";
        }
        
        // Упрощенная логика: если оба пути в одной папке, используем относительный путь
        var fromParts = fromPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var toParts = toPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Находим общий префикс
        int commonLength = 0;
        int minLength = Math.Min(fromParts.Length, toParts.Length);
        for (int i = 0; i < minLength; i++)
        {
            if (fromParts[i].Equals(toParts[i], StringComparison.OrdinalIgnoreCase))
            {
                commonLength++;
            }
            else
            {
                break;
            }
        }
        
        // Если есть общий префикс, строим относительный путь
        if (commonLength > 0)
        {
            var upLevels = fromParts.Length - commonLength;
            var downPath = string.Join("/", toParts.Skip(commonLength));
            var relativePath = string.Join("/", Enumerable.Repeat("..", upLevels));
            if (!string.IsNullOrEmpty(downPath))
            {
                relativePath = string.IsNullOrEmpty(relativePath) ? downPath : $"{relativePath}/{downPath}";
            }
            return $"{relativePath}/{fileName}";
        }
        
        // Если нет общего префикса, используем абсолютный путь
        return $"{toPath}/{fileName}";
    }
    
    /// <summary>
    /// Нормализует путь (убирает двойные слеши, заменяет обратные слеши)
    /// </summary>
    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "";
        
        return path
            .Replace('\\', '/')
            .Replace("//", "/")
            .TrimEnd('/');
    }

}
