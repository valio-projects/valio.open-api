using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Ast.Builders;
using ValIO.Generators.Cpp.Ast.Builders.Std;
using ValIO.Generators.Cpp.Ast.Renderers;

namespace ValIO.Generators.Cpp.Steps;

/// <summary>
/// Клиенты std-профиля: clientShape = class | interface+impl (I{Client}.h + {Client}Client);
/// apiStyle = sync | async | both. Тела операций — с именованными локальными
/// переменными (path/httpRequest/body/sendResult/response).
/// </summary>
public sealed class CppStdClientStep(ICppAstRenderer renderer) : CppStdStepBase(renderer)
{
    public override string Name => "cpp-std-clients";
    public override int Order => 80;

    public override bool ShouldExecute(GenerationContext context)
        => base.ShouldExecute(context)
           && context.Configuration.GenerateClients
           && context.IrDocument.Clients.Count > 0;

    public override Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var (naming, mapper) = CreateServices(context);
        DrainWarnings(context, mapper);
        var options = EffectiveOptions(context);
        var builder = new StdClientAstBuilder(mapper, naming);
        var (_, modelLookup) = CppAstBuilder.PrepareInputs(context.IrDocument);
        var enumNames = context.IrDocument.Enums.Select(e => e.Name).ToHashSet();
        var rootNamespace = ResolveRootNamespace(context);

        foreach (var client in context.IrDocument.Clients)
        {
            ct.ThrowIfCancellationRequested();
            var build = builder.Build(client, enumNames, modelLookup, rootNamespace, options);

            if (build.InterfaceHeader != null)
                context.GeneratedFiles.Add(new GeneratedFile(
                    naming.InterfaceHeaderPath(client.Name), Renderer.Render(build.InterfaceHeader)));
            context.GeneratedFiles.Add(new GeneratedFile(
                naming.ClientHeaderPath(client.Name), Renderer.Render(build.ClientHeader)));
            context.GeneratedFiles.Add(new GeneratedFile(
                naming.ClientSourcePath(client.Name), Renderer.Render(build.ClientSource)));
        }

        return Task.CompletedTask;
    }
}
