namespace ValIO.Core.Plugins;

public interface ICodeGenerationStep
{
    string Name { get; }
    int Order { get; }
    bool ShouldExecute(GenerationContext context);
    Task ExecuteAsync(GenerationContext context, CancellationToken ct = default);
}
