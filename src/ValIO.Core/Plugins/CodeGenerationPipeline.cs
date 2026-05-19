namespace ValIO.Core.Plugins;

public sealed class CodeGenerationPipeline : ICodeGenerationPipeline
{
    public IReadOnlyList<ICodeGenerationStep> Steps { get; }

    public CodeGenerationPipeline(IEnumerable<ICodeGenerationStep> steps)
    {
        Steps = [.. steps.OrderBy(s => s.Order)];
    }

    public async Task<IReadOnlyList<GeneratedFile>> ExecuteAsync(
        GenerationContext context,
        CancellationToken ct = default)
    {
        foreach (var step in Steps)
        {
            ct.ThrowIfCancellationRequested();
            if (step.ShouldExecute(context))
                await step.ExecuteAsync(context, ct);
        }
        return context.GeneratedFiles.AsReadOnly();
    }
}
