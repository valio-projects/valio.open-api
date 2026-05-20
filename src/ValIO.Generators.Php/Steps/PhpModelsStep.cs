using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.Plugins;
using ValIO.Generators.Php.Ast.Builders;
using ValIO.Generators.Php.Ast.Renderers;
using ValIO.Generators.Php.Options;

namespace ValIO.Generators.Php.Steps;

/// <summary>
/// Step: генерирует PHP DTO-классы из IR моделей.
/// </summary>
public sealed class PhpModelsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IPhpAstRenderer _renderer;
    private readonly PhpIrTypeMapper _typeMapper;

    public string Name => "php-models";
    public int Order => 10;

    public PhpModelsStep(INamingConverter namingConverter, IPhpAstRenderer renderer, PhpIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as PhpGenerationOptions ?? new PhpGenerationOptions();
        var builder = new PhpModelAstBuilder(_namingConverter, _typeMapper);

        var orderedNames = context.IrDocument.TypeDependencies.TopologicalOrder;
        var byName = context.IrDocument.Models.ToDictionary(m => m.Name);
        var processed = new HashSet<string>();

        foreach (var name in orderedNames)
        {
            if (!byName.TryGetValue(name, out var model)) continue;
            AddModelFile(context, builder, model, opts.Namespace);
            processed.Add(name);
        }

        foreach (var model in context.IrDocument.Models)
        {
            if (!processed.Contains(model.Name))
                AddModelFile(context, builder, model, opts.Namespace);
        }

        return Task.CompletedTask;
    }

    private void AddModelFile(GenerationContext context, PhpModelAstBuilder builder, IrModel model, string @namespace)
    {
        var sourceFile = builder.BuildSourceFile(model, @namespace);
        var content = _renderer.Render(sourceFile);
        var relativePath = $"Models/{model.Name}.php";
        context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
    }
}
