using System.IO.Compression;
using ValIO.Core.Plugins;
using ValIO.Output.Abstractions;
using ValIO.Output.Internal;

namespace ValIO.Output.Sinks;

/// <summary>
/// Writes generated files into a .zip archive on disk at <paramref name="path"/>.
/// Creates parent directories if they don't exist. Overwrites existing file.
/// </summary>
public sealed class ZipFileOutputSink : IGenerationOutputSink
{
    private readonly string _path;
    private FileStream? _file;
    private ZipArchive? _archive;

    public ZipFileOutputSink(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Zip file path is required", nameof(path));
        _path = path;
    }

    public async Task WriteAsync(IReadOnlyList<GeneratedFile> files, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _file = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None);
        _archive = new ZipArchive(_file, ZipArchiveMode.Create, leaveOpen: false);
        await ZipArchiveWriter.WriteAllAsync(_archive, files, ct);
    }

    public ValueTask DisposeAsync()
    {
        _archive?.Dispose();
        _archive = null;
        _file?.Dispose();
        _file = null;
        return ValueTask.CompletedTask;
    }
}
