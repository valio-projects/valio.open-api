using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Php.Ast.Builders;
using ValIO.Generators.Php.Ast.Renderers;
using ValIO.Generators.Php.Options;

namespace ValIO.Generators.Php.Steps;

/// <summary>
/// Step: генерирует PHP 8.1+ backed enums из IR.
/// </summary>
public sealed class PhpEnumsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IPhpAstRenderer _renderer;

    public string Name => "php-enums";
    public int Order => 20;

    public PhpEnumsStep(INamingConverter namingConverter, IPhpAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as PhpGenerationOptions ?? new PhpGenerationOptions();
        var builder = new PhpEnumAstBuilder(_namingConverter);

        foreach (var irEnum in context.IrDocument.Enums)
        {
            var sourceFile = builder.BuildSourceFile(irEnum, opts.Namespace);
            var content = _renderer.Render(sourceFile);
            var relativePath = $"Models/{irEnum.Name}.php";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
