using ValIO.Generators.CSharp.Ast.Builders;
using ValIO.Core.IR;
using ValIO.Core.Plugins;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Options;

namespace ValIO.Generators.CSharp.Steps;

/// <summary>Generates C# contracts and adapters around the official SignalR runtime.</summary>
/// <param name="renderer">The pipeline-owned C# compilation unit renderer.</param>
internal sealed class CSharpSignalRStep(ICSharpAstRenderer renderer) : ICodeGenerationStep
{
    /// <summary>Builds the complete C# tree from the resolved hub contract.</summary>
    private readonly CSharpSignalRAstBuilder _builder = new();

    /// <inheritdoc />
    public string Name => "csharp-signalr";

    /// <inheritdoc />
    public int Order => 40;

    /// <inheritdoc />
    public bool ShouldExecute(GenerationContext context) => context.IrDocument.SignalRHubs.Count > 0 &&
        (context.Configuration.GenerateClients || context.PluginOptions is CSharpGenerationOptions { GenerateSignalRHubs: true });

    /// <inheritdoc />
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var options = context.PluginOptions as CSharpGenerationOptions;
        var ns = options?.Namespace ?? context.Configuration.Namespace ?? "Generated";
        foreach (var hub in context.IrDocument.SignalRHubs)
        {
            ct.ThrowIfCancellationRequested();
            if (options?.GenerateSignalRHubs == true)
            {
                var name = hub.Name + "HubBase";
                EnsureDeclarationNames(context, hub, name, "I" + hub.Name + "ClientEvents");
                context.GeneratedFiles.Add(new GeneratedFile($"SignalR/{name}.cs", renderer.Render(_builder.BuildHub(hub, ns, name))));
            }

            if (context.Configuration.GenerateClients)
            {
                var name = hub.Name + "SignalRClient";
                EnsureDeclarationNames(context, hub, name);
                context.GeneratedFiles.Add(new GeneratedFile($"SignalR/{name}.cs", renderer.Render(_builder.BuildClient(hub, ns, name))));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Checks emitted names against models and generated members.</summary>
    /// <param name="context">The generation request with its model declarations.</param>
    /// <param name="hub">The hub being emitted.</param>
    /// <param name="names">The type names allocated to this hub artifact.</param>
    /// <exception cref="InvalidOperationException">A generated type or member name is already occupied.</exception>
    private static void EnsureDeclarationNames(GenerationContext context, IrSignalRHub hub, params string[] names)
    {
        foreach (var name in names)
        {
            if (context.DocumentContext.KnownTypeNames.Contains(name))
                throw new InvalidOperationException($"SignalR type '{name}' conflicts with a model or enum.");
        }

        var occupied = new HashSet<string>(names, StringComparer.Ordinal) { "HubPath", "ParameterType" };
        if (names.Length == 1)
        {
            foreach (var notification in hub.Events)
                occupied.Add("On" + notification.Name);
        }

        foreach (var call in hub.Methods)
        {
            if (!occupied.Add(call.Name))
                throw new InvalidOperationException($"SignalR method '{hub.Name}.{call.Name}' conflicts with a generated member.");
        }
    }
}
