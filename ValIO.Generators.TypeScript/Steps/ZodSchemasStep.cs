using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Types;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Zod;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Step: генерирует Zod-схемы для всех IR моделей и enum-ов.
/// Активируется при ValidationLibrary = "zod3" или "zod4".
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

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        var fileOrg = context.Configuration.FileOrganization;
        var modelsPath = fileOrg.ModelsPath ?? "models";
        var schemasPath = $"{modelsPath}/schemas";

        var zodVersion = opts?.ValidationLibrary ?? "zod3";
        var astBuilder = new TypeScriptAstBuilder(_namingConverter, opts);
        TypeNode TypeBuilder(ValIO.Core.IR.Types.IrType t) => astBuilder.BuildType(t);

        var renderer = new ZodSchemaRenderer(_namingConverter, TypeBuilder, zodVersion: zodVersion);

        var orderedNames = context.IrDocument.TypeDependencies.TopologicalOrder;
        var byName = context.IrDocument.Models.ToDictionary(m => m.Name);
        var processed = new HashSet<string>();

        foreach (var name in orderedNames)
        {
            if (!byName.TryGetValue(name, out var model)) continue;
            AddModelSchema(context, renderer, model, modelsPath, schemasPath, fileOrg.SeparateModels);
            processed.Add(name);
        }

        foreach (var model in context.IrDocument.Models)
        {
            if (!processed.Contains(model.Name))
                AddModelSchema(context, renderer, model, modelsPath, schemasPath, fileOrg.SeparateModels);
        }

        foreach (var irEnum in context.IrDocument.Enums)
        {
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
        var fileName = _namingConverter.ToCamelCase(model.Name);
        // importPath=null — renderer построит относительный путь к модели
        // из schemasPath + currentFileName сам.
        var content = renderer.RenderFromIr(model,
            importPath: null,
            currentFileName: fileName,
            schemasPath: schemasPath);

        var relativePath = separateModels
            ? $"{schemasPath}/{fileName}/{fileName}.schema.ts"
            : $"{schemasPath}/{fileName}.schema.ts";

        context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
    }

    private void AddEnumSchema(
        GenerationContext context,
        ZodSchemaRenderer renderer,
        ValIO.Core.IR.IrEnum irEnum,
        string modelsPath,
        string schemasPath,
        bool separateModels)
    {
        var fileName = _namingConverter.ToCamelCase(irEnum.Name);
        var content = renderer.RenderEnumFromIr(irEnum,
            importPath: null,
            currentFileName: fileName,
            schemasPath: schemasPath);

        var relativePath = separateModels
            ? $"{schemasPath}/{fileName}/{fileName}.schema.ts"
            : $"{schemasPath}/{fileName}.schema.ts";

        context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
    }
}
