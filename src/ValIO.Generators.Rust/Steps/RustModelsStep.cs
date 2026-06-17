using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Rust.Ast.Builders;
using ValIO.Generators.Rust.Ast.Renderers;
using ValIO.Generators.Rust.Options;

namespace ValIO.Generators.Rust.Steps;

/// <summary>
/// Generates one Rust file per IR model under <c>{crate}/src/models/{snake}.rs</c>.
/// </summary>
public sealed class RustModelsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IRustAstRenderer _renderer;
    private readonly RustIrTypeMapper _typeMapper;

    public string Name => "rust-models";
    public int Order => 10;

    public RustModelsStep(
        INamingConverter namingConverter,
        IRustAstRenderer renderer,
        RustIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as RustGenerationOptions ?? new RustGenerationOptions();
        var modelsDir = RustStepHelper.ModelsDir(opts);

        var builder = new RustModelAstBuilder(_namingConverter, _typeMapper);
        foreach (var model in context.IrDocument.Models)
        {
            var file = builder.BuildFile(model);
            var content = _renderer.Render(file);
            var snake = _namingConverter.ToSnakeCase(model.OriginalName);
            var relativePath = $"{modelsDir}/{snake}.rs";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
