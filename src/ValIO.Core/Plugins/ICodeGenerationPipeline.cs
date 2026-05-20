namespace ValIO.Core.Plugins;

public interface ICodeGenerationPipeline
{
    IReadOnlyList<ICodeGenerationStep> Steps { get; }
    Task<IReadOnlyList<GeneratedFile>> ExecuteAsync(GenerationContext context, CancellationToken ct = default);
}
