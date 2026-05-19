using System.Text;
using ValIO.Core.Plugins;
using ValIO.Output.Abstractions;

namespace ValIO.Output.Sinks;

/// <summary>
/// Writes generated files into a directory tree, creating intermediate directories as needed.
/// </summary>
public sealed class DiskOutputSink : IGenerationOutputSink
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _root;

    public DiskOutputSink(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path is required", nameof(rootPath));
        _root = rootPath;
    }

    public async Task WriteAsync(IReadOnlyList<GeneratedFile> files, CancellationToken ct = default)
    {
        EnsureDirectory(_root);
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var fullPath = Path.Combine(_root, file.RelativePath);
            EnsureDirectory(Path.GetDirectoryName(fullPath));
            await File.WriteAllTextAsync(fullPath, file.Content, Utf8NoBom, ct);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static void EnsureDirectory(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }
}
