using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.CSharp.Ast.Builders;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Options;

namespace ValIO.Generators.CSharp.Steps;

/// <summary>
/// Step: генерирует C# record/class из IR моделей.
/// </summary>
public sealed class CSharpModelsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly ICSharpAstRenderer _renderer;

    public string Name => "csharp-models";
    public int Order => 10;

    public CSharpModelsStep(INamingConverter namingConverter, ICSharpAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as CSharpGenerationOptions;
        var ns = opts?.Namespace ?? context.Configuration.Namespace ?? "Generated";
        var builder = new CSharpAstBuilder(_namingConverter);

        var orderedNames = context.IrDocument.TypeDependencies.TopologicalOrder;
        var byName = context.IrDocument.Models.ToDictionary(m => m.Name);
        var processed = new HashSet<string>();

        foreach (var name in orderedNames)
        {
            if (!byName.TryGetValue(name, out var model)) continue;
            AddModelFile(context, builder, model, ns);
            processed.Add(name);
        }

        foreach (var model in context.IrDocument.Models)
        {
            if (!processed.Contains(model.Name))
                AddModelFile(context, builder, model, ns);
        }

        return Task.CompletedTask;
    }

    private void AddModelFile(GenerationContext context, CSharpAstBuilder builder,
        ValIO.Core.IR.IrModel model, string ns)
    {
        var compilationUnit = builder.BuildCompilationUnit(model, ns);
        var content = _renderer.Render(compilationUnit);
        var relativePath = $"Models/{model.OriginalName}.cs";
        context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
    }
}
