using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Runtime;

namespace ValIO.Generators.Cpp.Steps;

/// <summary>
/// Самодостаточный ValIO-runtime std-профиля — эмитится ровно один раз на проект.
/// Файлы канонические (raw-строки, прецедент CSharpClientStep): компилируемость
/// на C++17/20/23 против cpp-httplib 0.18.3 + nlohmann_json 3.12.0 проверяет
/// compile-фикстура (W6).
/// </summary>
public sealed class CppStdRuntimeStep(ICppAstRenderer renderer) : CppStdStepBase(renderer)
{
    public override string Name => "cpp-std-runtime";
    public override int Order => 70;

    public override bool ShouldExecute(GenerationContext context)
        => base.ShouldExecute(context)
           && (context.IrDocument.Models.Count > 0 || context.IrDocument.Clients.Count > 0);

    public override Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var options = EffectiveOptions(context);
        var alreadyEmitted = context.GeneratedFiles.Any(f => f.RelativePath == StdRuntimeTemplates.ExpectedHeaderFileName);
        if (alreadyEmitted)
            return Task.CompletedTask;

        ct.ThrowIfCancellationRequested();
        context.GeneratedFiles.Add(new GeneratedFile(StdRuntimeTemplates.ExpectedHeaderFileName, StdRuntimeTemplates.ExpectedHeader()));
        context.GeneratedFiles.Add(new GeneratedFile(StdRuntimeTemplates.ErrorHeaderFileName, StdRuntimeTemplates.ErrorHeader()));
        context.GeneratedFiles.Add(new GeneratedFile(StdRuntimeTemplates.HttpHeaderFileName, StdRuntimeTemplates.HttpHeader()));
        context.GeneratedFiles.Add(new GeneratedFile(StdRuntimeTemplates.HttpSourceFileName, StdRuntimeTemplates.HttpSource(options.HttpInclude)));
        context.GeneratedFiles.Add(new GeneratedFile(StdRuntimeTemplates.JsonHeaderFileName, StdRuntimeTemplates.JsonHeader(options.JsonInclude)));
        return Task.CompletedTask;
    }
}
