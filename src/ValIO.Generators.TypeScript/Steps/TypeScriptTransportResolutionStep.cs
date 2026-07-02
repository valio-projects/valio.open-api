using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Early pipeline step that resolves which client transport the query hooks wrap and — when necessary —
/// auto-adds it to the selected transports so its client module is actually emitted, recording a non-blocking
/// warning about the adjustment.
/// </summary>
/// <remarks>
/// <para>
/// Query hooks (TanStack Query / SWR) import a plain client function from <c>./{op}.{suffix}</c> where the suffix
/// is one of ky/axios/fetch (see <see cref="TypeScriptClientTransport.ResolveHookClientSuffix"/>). If a wrapper is
/// selected but no client-capable transport is present — or an explicit <c>queryTransport</c> is chosen that is not
/// among the selected transports — the hooks would import a module that is never generated and the output would
/// fail to compile. This step closes that gap by adding the resolved transport to
/// <see cref="TypeScriptGenerationOptions.Transports"/> before the client/hook steps run.
/// </para>
/// <para>
/// It runs at a very low <see cref="Order"/> (before every client, query-keys and framework-adapter step) and
/// mutates the shared options instance, relying on the pipeline evaluating <c>ShouldExecute</c> lazily per step so
/// the later steps observe the adjusted transport list.
/// </para>
/// </remarks>
/// <seealso cref="TypeScriptClientTransport"/>
public sealed class TypeScriptTransportResolutionStep : ICodeGenerationStep
{
    /// <summary>The step name.</summary>
    public string Name => "typescript-transport-resolution";

    /// <summary>Runs before every client (Fetch=40 …), query-keys (48) and TanStack (50+) step.</summary>
    public int Order => 5;

    /// <inheritdoc/>
    /// <remarks>
    /// Only relevant when clients are generated and a query wrapper is selected or a <c>queryTransport</c> is set —
    /// the resolution/auto-fix has no effect otherwise.
    /// </remarks>
    public bool ShouldExecute(GenerationContext context)
    {
        if (!context.Configuration.GenerateClients)
            return false;
        if (context.PluginOptions is not TypeScriptGenerationOptions opts)
            return false;
        return !string.IsNullOrWhiteSpace(opts.QueryWrapper) || !string.IsNullOrWhiteSpace(opts.QueryTransport);
    }

    /// <inheritdoc/>
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        if (context.PluginOptions is not TypeScriptGenerationOptions opts)
            return Task.CompletedTask;

        var desired = TypeScriptClientTransport.ResolveHookClientSuffix(opts);
        var alreadySelected = opts.Transports.Any(t => string.Equals(t, desired, StringComparison.OrdinalIgnoreCase));
        if (alreadySelected)
            return Task.CompletedTask;

        opts.Transports.Add(desired);

        var explicitlyChosen = TypeScriptClientTransport.IsGeneratedClientSuffix(opts.QueryTransport);
        context.Warnings.Add(explicitlyChosen
            ? $"queryTransport '{desired}' is not in the selected transports; added it so the query hooks resolve their client. Add '{desired}' to 'transports' to silence this."
            : $"Query wrapper '{opts.QueryWrapper}' was selected but no client-capable transport (ky/axios/fetch) was chosen; added '{desired}' so the generated hooks resolve their client. Add ky/axios/fetch to 'transports' (or set 'queryTransport') to control this.");

        return Task.CompletedTask;
    }
}
