using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Java.Ast.Builders;
using ValIO.Generators.Java.Ast.Renderers;
using ValIO.Generators.Java.Options;

namespace ValIO.Generators.Java.Steps;

/// <summary>
/// Step: генерирует Java enum для каждого IR-enum'а (plain или backed).
/// </summary>
public sealed class JavaEnumsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IJavaAstRenderer _renderer;

    public string Name => "java-enums";
    public int Order => 20;

    public JavaEnumsStep(INamingConverter namingConverter, IJavaAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as JavaGenerationOptions ?? new JavaGenerationOptions();
        var builder = new JavaEnumAstBuilder(_namingConverter);

        foreach (var irEnum in context.IrDocument.Enums)
        {
            var sourceFile = builder.BuildSourceFile(irEnum, opts.Package);
            var content = _renderer.Render(sourceFile);
            var relativePath = $"models/{irEnum.Name}.java";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
