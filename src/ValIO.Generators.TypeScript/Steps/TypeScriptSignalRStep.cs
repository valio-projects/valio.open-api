using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>Emits typed invocations and subscriptions over an existing JavaScript SignalR connection.</summary>
/// <param name="naming">The converter used to locate generated model modules.</param>
/// <param name="nodeRenderers">The node renderers registered for this language pipeline.</param>
internal sealed class TypeScriptSignalRStep(INamingConverter naming, IEnumerable<ITypeScriptNodeRenderer> nodeRenderers)
    : ICodeGenerationStep
{
    /// <summary>Renders the complete client module with the profile's language syntax.</summary>
    private readonly TypeScriptAstRenderer _renderer = new(nodeRenderers);

    /// <inheritdoc />
    public string Name => "typescript-signalr";

    /// <inheritdoc />
    public int Order => 65;

    /// <inheritdoc />
    public bool ShouldExecute(GenerationContext context) =>
        context.Configuration.GenerateClients && context.IrDocument.SignalRHubs.Count > 0;

    /// <inheritdoc />
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var builder = new TypeScriptSignalRAstBuilder(naming);
        foreach (var hub in context.IrDocument.SignalRHubs)
        {
            ct.ThrowIfCancellationRequested();
            var fileName = naming.ToCamelCase(hub.Name) + "SignalR";
            var module = builder.Build(context, hub);
            TypeScriptModuleFiles.Emit(context, $"signalr/{fileName}.ts", module, _renderer.Render);
        }

        return Task.CompletedTask;
    }
}
