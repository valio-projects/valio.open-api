using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Ast.Builders;
using ValIO.Generators.Cpp.Ast.Builders.Std;
using ValIO.Generators.Cpp.Ast.Renderers;

namespace ValIO.Generators.Cpp.Steps;

/// <summary>
/// Модели std-профиля: Models/{N}.h — чистые header-only struct'ы без JSON-зависимостей.
/// </summary>
public sealed class CppStdModelsStep(ICppAstRenderer renderer) : CppStdStepBase(renderer)
{
    public override string Name => "cpp-std-models";
    public override int Order => 50;

    public override bool ShouldExecute(GenerationContext context)
        => base.ShouldExecute(context)
           && context.Configuration.GenerateModels
           && context.IrDocument.Models.Count > 0;

    /// <inheritdoc />
    public override Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var (naming, mapper) = CreateServices(context);
        var builder = new StdModelAstBuilder(mapper, naming);
        var (_, modelLookup) = CppAstBuilder.PrepareInputs(context.IrDocument);
        var enumNames = context.IrDocument.Enums.Select(e => e.Name).ToHashSet();
        var rootNamespace = ResolveRootNamespace(context);

        foreach (var model in context.IrDocument.Models)
        {
            ct.ThrowIfCancellationRequested();
            var header = builder.Build(model, enumNames, modelLookup, rootNamespace, context.Warnings);
            context.GeneratedFiles.Add(new GeneratedFile(
                naming.ModelHeaderPath(model.OriginalName), Renderer.Render(header)));
        }

        DrainWarnings(context, mapper);
        return Task.CompletedTask;
    }
}
