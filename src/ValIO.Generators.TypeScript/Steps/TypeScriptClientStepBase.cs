using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Shared boiler-plate for per-operation TypeScript transport Steps.
/// Concrete steps (Fetch, Axios, Ky, Undici, OFetch, Got, SolidStart, …)
/// only need to declare:
/// <list type="bullet">
///   <item>the transport id they react to (<see cref="TransportId"/>),</item>
///   <item>the file suffix to emit (<see cref="FileSuffix"/>),</item>
///   <item>how to build a <see cref="SourceFileNode"/> for one operation
///     (<see cref="BuildOperationSourceFile"/>).</item>
/// </list>
///
/// Everything else — naming, rendering, path composition, cancellation —
/// lives here so the contract stays uniform across transports.
/// </summary>
public abstract class TypeScriptClientStepBase : ICodeGenerationStep
{
    /// <summary>Naming converter used for the file name (camelCase by default).</summary>
    protected INamingConverter NamingConverter { get; }

    /// <summary>Renderer instance built once per Step with the Chain-of-Responsibility node renderers.</summary>
    protected TypeScriptAstRenderer Renderer { get; }

    /// <summary>
    /// Models from the active IR document, keyed by generated model name, for transport builders that need request
    /// body property metadata such as multipart field names.
    /// </summary>
    protected IReadOnlyDictionary<string, IrModel> ModelsByName { get; private set; } =
        new Dictionary<string, IrModel>(StringComparer.Ordinal);

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract int Order { get; }

    /// <summary>Transport id this step reacts to (e.g. "fetch", "axios", "ky").</summary>
    protected abstract string TransportId { get; }

    /// <summary>File suffix including dot, e.g. <c>.ky.ts</c>.</summary>
    protected abstract string FileSuffix { get; }

    /// <summary>
    /// When <c>true</c>, the step also runs if <see cref="TypeScriptGenerationOptions"/>
    /// or its <see cref="TypeScriptGenerationOptions.Transports"/> list is <c>null</c>
    /// (so out-of-the-box generation works without configuration).
    /// An <em>explicit empty</em> list still suppresses generation.
    /// Only <see cref="FetchClientStep"/> sets this to <c>true</c>.
    /// </summary>
    protected virtual bool ActivateWhenOptionsMissing => false;

    protected TypeScriptClientStepBase(
        INamingConverter namingConverter,
        IEnumerable<ITypeScriptNodeRenderer>? nodeRenderers = null)
    {
        NamingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        Renderer = new TypeScriptAstRenderer(nodeRenderers ?? Array.Empty<ITypeScriptNodeRenderer>());
    }

    /// <inheritdoc />
    public virtual bool ShouldExecute(GenerationContext context)
    {
        if (!context.Configuration.GenerateClients) return false;
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        // Options missing entirely → activate iff this step opts in as the default.
        if (opts == null) return ActivateWhenOptionsMissing;
        // Transport-list missing (null) is also "default" — same opt-in rule.
        if (opts.Transports == null) return ActivateWhenOptionsMissing;
        // Otherwise — explicit list (possibly empty) — only the listed transports run.
        return opts.Transports.Contains(TransportId, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        var fileOrg = context.Configuration.FileOrganization;
        var clientsPath = fileOrg.ClientsPath ?? "api";
        ModelsByName = context.IrDocument.Models.ToDictionary(model => model.Name, StringComparer.Ordinal);

        foreach (var client in context.IrDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                ct.ThrowIfCancellationRequested();

                var sourceFile = BuildOperationSourceFile(operation, fileOrg, opts);
                var content = Renderer.Render(sourceFile);
                var fileName = NamingConverter.ToCamelCase(operation.OriginalMethodName);
                var relativePath = $"{clientsPath}/{fileName}/{fileName}{FileSuffix}";
                context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Build a <see cref="SourceFileNode"/> for a single operation. Implementations
    /// instantiate their transport-specific AST builder and delegate.
    /// </summary>
    protected abstract SourceFileNode BuildOperationSourceFile(
        IrOperation operation,
        FileOrganization fileOrg,
        TypeScriptGenerationOptions? options);
}
