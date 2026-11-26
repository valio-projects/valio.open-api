using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Zod;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Zod.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Generates Zod schemas for the shared document's models and enums.
/// </summary>
public sealed class ZodSchemasStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;

    public string Name => "typescript-zod-schemas";
    public int Order => 30;

    public ZodSchemasStep(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public bool ShouldExecute(GenerationContext context)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        return opts?.ValidationLibrary is "zod3" or "zod4";
    }

    /// <inheritdoc />
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        var fileOrg = context.Configuration.FileOrganization;
        var modelsPath = fileOrg.ModelsPath ?? "models";
        var schemasPath = fileOrg.SchemasPath ?? "schemas";
        var documentContext = context.DocumentContext;

        var zodVersion = opts?.ValidationLibrary ?? "zod3";
        var astBuilder = new TypeScriptAstBuilder(documentContext, _namingConverter, opts);

        var renderer = new ZodSchemaRenderer(documentContext, _namingConverter, astBuilder.BuildType, zodVersion: zodVersion,
            modelsPath: modelsPath, uuidFormat: opts?.UuidFormat, exactJsonNumbers: opts?.ExactJsonNumbers == true);

        foreach (var model in documentContext.ModelsInDependencyOrder)
        {
            ct.ThrowIfCancellationRequested();
            AddModelSchema(context, renderer, model, modelsPath, schemasPath, fileOrg.SeparateModels);
        }

        foreach (var irEnum in documentContext.Document.Enums)
        {
            ct.ThrowIfCancellationRequested();
            AddEnumSchema(context, renderer, irEnum, modelsPath, schemasPath, fileOrg.SeparateModels);
        }

        return Task.CompletedTask;
    }

    private void AddModelSchema(
        GenerationContext context,
        ZodSchemaRenderer renderer,
        ValIO.Core.IR.IrModel model,
        string modelsPath,
        string schemasPath,
        bool separateModels)
    {
        var fileName = _namingConverter.ToCamelCase(model.OriginalName);
        // importPath=null — renderer построит относительный путь к модели
        // из schemasPath + currentFileName сам.
        var sourceFile = renderer.BuildModel(model,
            importPath: null,
            currentFileName: fileName,
            schemasPath: schemasPath);

        // Co-locate the schema with its model under modelsPath (not the separate schemas/ tree).
        var relativePath = separateModels
            ? $"{modelsPath}/{fileName}/{fileName}.schema.ts"
            : $"{modelsPath}/{fileName}.schema.ts";

        TypeScriptModuleFiles.Emit(context, relativePath, sourceFile, new TypeScriptAstRenderer([new ZodNodeRenderer()]).Render);
    }

    private void AddEnumSchema(
        GenerationContext context,
        ZodSchemaRenderer renderer,
        ValIO.Core.IR.IrEnum irEnum,
        string modelsPath,
        string schemasPath,
        bool separateModels)
    {
        var fileName = _namingConverter.ToCamelCase(irEnum.OriginalName);
        var sourceFile = renderer.BuildEnum(irEnum,
            importPath: null,
            currentFileName: fileName,
            schemasPath: schemasPath);

        // Co-locate the schema with its model under modelsPath (not the separate schemas/ tree).
        var relativePath = separateModels
            ? $"{modelsPath}/{fileName}/{fileName}.schema.ts"
            : $"{modelsPath}/{fileName}.schema.ts";

        TypeScriptModuleFiles.Emit(context, relativePath, sourceFile, new TypeScriptAstRenderer([new ZodNodeRenderer()]).Render);
    }
}
