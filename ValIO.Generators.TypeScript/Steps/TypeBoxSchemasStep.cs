using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.TypeBox.Ast.Builders;

namespace ValIO.Generators.TypeScript.Steps;

public sealed class TypeBoxSchemasStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IReadOnlyList<ITypeScriptNodeRenderer> _nodeRenderers;

    public string Name => "typescript-typebox-schemas";
    public int Order => 31;

    public TypeBoxSchemasStep(INamingConverter namingConverter)
        : this(namingConverter, Array.Empty<ITypeScriptNodeRenderer>())
    {
    }

    public TypeBoxSchemasStep(INamingConverter namingConverter, IEnumerable<ITypeScriptNodeRenderer> nodeRenderers)
    {
        _namingConverter = namingConverter;
        _nodeRenderers = nodeRenderers as IReadOnlyList<ITypeScriptNodeRenderer> ?? nodeRenderers.ToArray();
    }

    public bool ShouldExecute(GenerationContext context)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        return opts?.ValidationLibrary == "typebox";
    }

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var fileOrg = context.Configuration.FileOrganization;
        var modelsPath = fileOrg.ModelsPath ?? "models";
        var schemasPath = $"{modelsPath}/schemas";
        var builder = new TypeBoxSchemaSourceFileBuilder(_namingConverter);
        var renderer = new TypeScriptAstRenderer(_nodeRenderers);

        var orderedNames = context.IrDocument.TypeDependencies.TopologicalOrder;
        var byName = context.IrDocument.Models.ToDictionary(m => m.Name);
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
        TypeBoxSchemaSourceFileBuilder builder,
        TypeScriptAstRenderer renderer,
        ValIO.Core.IR.IrModel model,
        string schemasPath,
        bool separateModels)
    {
        var fileName = _namingConverter.ToCamelCase(model.Name);
        var content = renderer.Render(builder.BuildModelSchemaSourceFile(
            model,
            currentFileName: fileName,
            schemasPath: schemasPath));

        var relativePath = separateModels
            ? $"{schemasPath}/{fileName}/{fileName}.schema.ts"
            : $"{schemasPath}/{fileName}.schema.ts";

        context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
    }

    private void AddEnumSchema(
        GenerationContext context,
        TypeBoxSchemaSourceFileBuilder builder,
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
