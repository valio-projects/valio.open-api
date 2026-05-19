using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.Plugins;
using ValIO.Generators.Java.Ast.Builders;
using ValIO.Generators.Java.Ast.Renderers;
using ValIO.Generators.Java.Options;

namespace ValIO.Generators.Java.Steps;

/// <summary>
/// Step: генерирует Java record-DTO для каждой IR-модели.
/// </summary>
public sealed class JavaModelsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IJavaAstRenderer _renderer;
    private readonly JavaIrTypeMapper _typeMapper;

    public string Name => "java-models";
    public int Order => 10;

    public JavaModelsStep(INamingConverter namingConverter, IJavaAstRenderer renderer, JavaIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as JavaGenerationOptions ?? new JavaGenerationOptions();
        var builder = new JavaRecordModelAstBuilder(_namingConverter, _typeMapper);

        var orderedNames = context.IrDocument.TypeDependencies.TopologicalOrder;
        var byName = context.IrDocument.Models.ToDictionary(m => m.Name);
        var processed = new HashSet<string>();

        foreach (var name in orderedNames)
        {
            if (!byName.TryGetValue(name, out var model)) continue;
            AddModelFile(context, builder, model, opts.Package);
            processed.Add(name);
        }

        foreach (var model in context.IrDocument.Models)
        {
            if (!processed.Contains(model.Name))
                AddModelFile(context, builder, model, opts.Package);
        }

        return Task.CompletedTask;
    }

    private void AddModelFile(GenerationContext context, JavaRecordModelAstBuilder builder, IrModel model, string rootPackage)
    {
        var sourceFile = builder.BuildSourceFile(model, rootPackage);
        var content = _renderer.Render(sourceFile);
        var relativePath = $"models/{model.Name}.java";
        context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
    }
}
