using ValIO.Core.Models;

namespace ValIO.Application.Services;

/// <summary>
/// Entry point for generation. Parses the OpenAPI schema, builds IR, runs the plugin pipeline,
/// and returns the generated files. Does NOT write anything to disk.
/// </summary>
public interface IGenerationOrchestrator
{
    Task<OrchestrationResult> GenerateAsync(
        GenerationConfiguration config,
        string schemaPath,
        CancellationToken ct = default);
}
