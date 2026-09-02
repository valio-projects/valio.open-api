using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Ast.Builders;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Steps;

/// <summary>
/// Модели UE-профиля: Public/Models/{Model}.h (USTRUCT) для каждой IrModel.
/// </summary>
public sealed class CppUeModelsStep(ICppAstRenderer renderer) : CppUeStepBase(renderer)
{
    public override string Name => "cpp-ue-models";
    public override int Order => 10;

    public override bool ShouldExecute(GenerationContext context)
        => base.ShouldExecute(context)
           && context.Configuration.GenerateModels
           && context.IrDocument.Models.Count > 0;

    public override Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var builder = CreateBuilder(context);
        var naming = new UENamingConvention();
        var (enumNames, modelLookup) = CppAstBuilder.PrepareInputs(context.IrDocument);

        foreach (var model in context.IrDocument.Models)
        {
            ct.ThrowIfCancellationRequested();
            var header = builder.BuildModelHeader(model, enumNames, modelLookup);
            var content = Renderer.Render(header);
            context.GeneratedFiles.Add(new GeneratedFile(naming.ModelFullPath(model.OriginalName), content));
        }

        return Task.CompletedTask;
    }
}
