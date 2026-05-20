using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.FSharp.Ast.Builders;
using ValIO.Generators.FSharp.Ast.Renderers;
using ValIO.Generators.FSharp.Options;

namespace ValIO.Generators.FSharp.Steps;

/// <summary>
/// Generates one F# file per IR model (record or DU depending on composition).
/// </summary>
public sealed class FSharpModelsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IFSharpAstRenderer _renderer;
    private readonly FSharpIrTypeMapper _typeMapper;

    public string Name => "fsharp-models";
    public int Order => 10;

    public FSharpModelsStep(
        INamingConverter namingConverter,
        IFSharpAstRenderer renderer,
        FSharpIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as FSharpGenerationOptions ?? new FSharpGenerationOptions();
        var builder = new FSharpModelAstBuilder(_namingConverter, _typeMapper);

        foreach (var model in context.IrDocument.Models)
        {
            var typeName = _namingConverter.ToPascalCase(model.Name);
            var moduleName = FSharpStepHelper.ModelModuleName(opts, typeName);
            var file = builder.BuildFile(model, moduleName);
            var content = _renderer.Render(file);
            var relativePath = $"{FSharpStepHelper.ModelsDir}/{typeName}.fs";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
