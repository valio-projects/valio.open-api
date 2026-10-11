using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Ast.Builders;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Models;

namespace ValIO.Generators.Cpp.Steps;

/// <summary>
/// Single pipeline step that generates all C++/UE files (models, enums, params, results, endpoints)
/// using CppAstBuilder and CppAstRenderer.
/// </summary>
public sealed class CppGenerationStep : ICodeGenerationStep
{
    public string Name => "cpp-all";
    public int Order => 10;

    public bool ShouldExecute(GenerationContext context) => true;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as CppGenerationOptions ?? new CppGenerationOptions();
        var builder = new CppAstBuilder(opts);
        var renderer = new CppAstRenderer();

        var buildResult = builder.Build(context.IrDocument);

        foreach (var (relativePath, astNode) in buildResult.Files)
        {
            ct.ThrowIfCancellationRequested();
            var content = renderer.Render(astNode);
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
