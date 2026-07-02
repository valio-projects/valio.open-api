using ValIO.Core.IR;
using ValIO.Core.Models;

namespace ValIO.Core.Plugins;

public sealed class GenerationContext
{
    public required IrDocument IrDocument { get; init; }
    public required GenerationConfiguration Configuration { get; init; }
    public object? PluginOptions { get; init; }
    public List<GeneratedFile> GeneratedFiles { get; } = [];
    public Dictionary<string, object> StepData { get; } = [];

    /// <summary>
    /// Non-blocking diagnostics contributed by generation steps (e.g. a configuration auto-fix or a
    /// compatibility notice). The orchestrator drains these after the pipeline runs, logs them, and surfaces
    /// them in <c>OrchestrationResult.Warnings</c>. Steps append via <c>context.Warnings.Add(...)</c>.
    /// </summary>
    public List<string> Warnings { get; } = [];
}
