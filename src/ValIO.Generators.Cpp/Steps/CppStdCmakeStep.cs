using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Runtime;

namespace ValIO.Generators.Cpp.Steps;

/// <summary>
/// CMake-сниппет корня вывода std-профиля (опция emitCmake): glob исходников,
/// target valio_generated, find_package(httplib)/find_package(nlohmann_json),
/// целевой стандарт из cxxStandard. Потребитель — add_subdirectory + link.
/// </summary>
public sealed class CppStdCmakeStep(ICppAstRenderer renderer) : CppStdStepBase(renderer)
{
    public override string Name => "cpp-std-cmake";
    public override int Order => 90;

    public override bool ShouldExecute(GenerationContext context)
        => base.ShouldExecute(context)
           && EffectiveOptions(context).EmitCmake
           && (context.IrDocument.Models.Count > 0 || context.IrDocument.Clients.Count > 0);

    public override Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var options = EffectiveOptions(context);
        var content = StdRuntimeTemplates.CmakeLists(options.CxxStandard, ResolveRootNamespace(context));
        context.GeneratedFiles.Add(new GeneratedFile(StdRuntimeTemplates.CmakeListsFileName, content));
        return Task.CompletedTask;
    }
}
