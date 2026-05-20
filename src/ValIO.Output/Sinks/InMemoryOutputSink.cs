using ValIO.Core.Plugins;
using ValIO.Output.Abstractions;

namespace ValIO.Output.Sinks;

/// <summary>
/// Keeps generated files in memory. After <see cref="WriteAsync"/> the consumer can read them
/// from <see cref="Files"/> as an <see cref="IReadOnlyDictionary{TKey, TValue}"/> keyed by relative path.
/// </summary>
public sealed class InMemoryOutputSink : IGenerationOutputSink
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Files => _files;

    public Task WriteAsync(IReadOnlyList<GeneratedFile> files, CancellationToken ct = default)
    {
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            _files[file.RelativePath] = file.Content;
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
