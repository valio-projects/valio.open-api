using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Kotlin.Ast.Builders;
using ValIO.Generators.Kotlin.Ast.Renderers;
using ValIO.Generators.Kotlin.Options;

namespace ValIO.Generators.Kotlin.Steps;

/// <summary>
/// Generates one Kotlin file per IR model (data class + <c>@Serializable</c>).
/// </summary>
public sealed class KotlinModelsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IKotlinAstRenderer _renderer;
    private readonly KotlinIrTypeMapper _typeMapper;

    public string Name => "kotlin-models";
    public int Order => 10;

    public KotlinModelsStep(
        INamingConverter namingConverter,
        IKotlinAstRenderer renderer,
        KotlinIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as KotlinGenerationOptions ?? new KotlinGenerationOptions();
        var modelsPackage = KotlinStepHelper.ModelsPackage(opts);
        var modelsDir = KotlinStepHelper.PackageToPath(modelsPackage);
        var knownNames = new HashSet<string>(
            context.IrDocument.Models.Select(m => m.Name)
                .Concat(context.IrDocument.Enums.Select(e => e.Name)),
            StringComparer.Ordinal);

        var builder = new KotlinDataClassAstBuilder(_namingConverter, _typeMapper);
        foreach (var model in context.IrDocument.Models)
        {
            var file = builder.BuildFile(model, modelsPackage, knownNames);
            var content = _renderer.Render(file);
            var className = _namingConverter.ToPascalCase(model.Name);
            var relativePath = $"{modelsDir}/{className}.kt";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
