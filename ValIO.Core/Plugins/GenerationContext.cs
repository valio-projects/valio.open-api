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
}
