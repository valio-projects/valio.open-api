using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Rust.Ast.Builders;
using ValIO.Generators.Rust.Ast.Renderers;
using ValIO.Generators.Rust.Options;

namespace ValIO.Generators.Rust.Steps;

/// <summary>
/// Generates one Rust file per IR enum under <c>{crate}/src/models/{snake}.rs</c>.
/// </summary>
public sealed class RustEnumsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IRustAstRenderer _renderer;

    public string Name => "rust-enums";
    public int Order => 20;

    public RustEnumsStep(INamingConverter namingConverter, IRustAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as RustGenerationOptions ?? new RustGenerationOptions();
        var modelsDir = RustStepHelper.ModelsDir(opts);

        var builder = new RustEnumAstBuilder(_namingConverter);
        foreach (var irEnum in context.IrDocument.Enums)
        {
            var file = builder.BuildFile(irEnum);
            var content = _renderer.Render(file);
            var snake = _namingConverter.ToSnakeCase(irEnum.Name);
            var relativePath = $"{modelsDir}/{snake}.rs";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
