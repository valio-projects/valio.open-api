using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Valibot.Ast.Builders;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Step that generates Valibot schemas for IR models and enums.
/// </summary>
public sealed class ValibotSchemasStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;

    public string Name => "typescript-valibot-schemas";
    public int Order => 30;

    public ValibotSchemasStep(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public bool ShouldExecute(GenerationContext context)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        return opts?.ValidationLibrary == "valibot";
    }

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var fileOrg = context.Configuration.FileOrganization;
        var modelsPath = fileOrg.ModelsPath ?? "models";
        var schemasPath = $"{modelsPath}/schemas";
        var builder = new ValibotSchemaSourceFileBuilder(_namingConverter);
        var renderer = new TypeScriptAstRenderer();

        var orderedNames = context.IrDocument.TypeDependencies.TopologicalOrder;
        var byName = context.IrDocument.Models.ToDictionary(model => model.Name);
        var processed = new HashSet<string>();

        foreach (var name in orderedNames)
        {
            ct.ThrowIfCancellationRequested();

            if (!byName.TryGetValue(name, out var model))
                continue;

            AddModelSchema(context, builder, renderer, model, schemasPath, fileOrg.SeparateModels);
            processed.Add(name);
        }

        foreach (var model in context.IrDocument.Models)
        {
            ct.ThrowIfCancellationRequested();

            if (!processed.Contains(model.Name))
                AddModelSchema(context, builder, renderer, model, schemasPath, fileOrg.SeparateModels);
        }

        foreach (var irEnum in context.IrDocument.Enums)
        {
            ct.ThrowIfCancellationRequested();
            AddEnumSchema(context, builder, renderer, irEnum, schemasPath, fileOrg.SeparateModels);
        }

        return Task.CompletedTask;
    }

    private void AddModelSchema(
        GenerationContext context,
        ValibotSchemaSourceFileBuilder builder,
        TypeScriptAstRenderer renderer,
        ValIO.Core.IR.IrModel model,
        string schemasPath,
        bool separateModels)
    {
        var content = renderer.Render(builder.BuildModelSchemaSourceFile(model, separateModels));
        var fileName = _namingConverter.ToCamelCase(model.Name);
        var relativePath = separateModels
            ? $"{schemasPath}/{fileName}/{fileName}.schema.ts"
            : $"{schemasPath}/{fileName}.schema.ts";

        context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
    }

    private void AddEnumSchema(
        GenerationContext context,
        ValibotSchemaSourceFileBuilder builder,
        TypeScriptAstRenderer renderer,
        ValIO.Core.IR.IrEnum irEnum,
        string schemasPath,
        bool separateModels)
    {
        var content = renderer.Render(builder.BuildEnumSchemaSourceFile(irEnum));
        var fileName = _namingConverter.ToCamelCase(irEnum.Name);
        var relativePath = separateModels
            ? $"{schemasPath}/{fileName}/{fileName}.schema.ts"
            : $"{schemasPath}/{fileName}.schema.ts";

        context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
    }
}
