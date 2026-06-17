using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Kotlin.Ast.Builders;
using ValIO.Generators.Kotlin.Ast.Renderers;
using ValIO.Generators.Kotlin.Options;

namespace ValIO.Generators.Kotlin.Steps;

/// <summary>
/// Generates one Kotlin file per IR enum (<c>@Serializable enum class</c>).
/// </summary>
public sealed class KotlinEnumsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IKotlinAstRenderer _renderer;

    public string Name => "kotlin-enums";
    public int Order => 20;

    public KotlinEnumsStep(INamingConverter namingConverter, IKotlinAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as KotlinGenerationOptions ?? new KotlinGenerationOptions();
        var modelsPackage = KotlinStepHelper.ModelsPackage(opts);
        var modelsDir = KotlinStepHelper.PackageToPath(modelsPackage);

        var builder = new KotlinEnumAstBuilder(_namingConverter);
        foreach (var irEnum in context.IrDocument.Enums)
        {
            var file = builder.BuildFile(irEnum, modelsPackage);
            var content = _renderer.Render(file);
            // File name uses the ORIGINAL (pre-affix) name so files don't move when an
            // affix is applied; the emitted enum identifier stays affixed (see builder).
            var fileName = _namingConverter.ToPascalCase(irEnum.OriginalName);
            var relativePath = $"{modelsDir}/{fileName}.kt";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
