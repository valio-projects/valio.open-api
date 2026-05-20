using ValIO.Core.Plugins;

namespace ValIO.Output.Abstractions;

/// <summary>
/// Receives the generated files and materializes them somewhere (disk, zip, memory, ...).
/// </summary>
public interface IGenerationOutputSink : IAsyncDisposable
{
    Task WriteAsync(IReadOnlyList<GeneratedFile> files, CancellationToken ct = default);
}
