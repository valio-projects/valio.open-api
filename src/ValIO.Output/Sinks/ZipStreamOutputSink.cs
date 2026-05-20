using System.IO.Compression;
using ValIO.Core.Plugins;
using ValIO.Output.Abstractions;
using ValIO.Output.Internal;

namespace ValIO.Output.Sinks;

/// <summary>
/// Writes generated files into a .zip archive backed by a caller-provided <see cref="Stream"/>.
/// Useful when the caller wants to stream the zip directly to HTTP response body or a memory stream.
/// </summary>
public sealed class ZipStreamOutputSink : IGenerationOutputSink
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private ZipArchive? _archive;

    public ZipStreamOutputSink(Stream stream, bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite) throw new ArgumentException("Stream must be writable", nameof(stream));
        _stream = stream;
        _leaveOpen = leaveOpen;
    }

    public async Task WriteAsync(IReadOnlyList<GeneratedFile> files, CancellationToken ct = default)
    {
        _archive = new ZipArchive(_stream, ZipArchiveMode.Create, leaveOpen: _leaveOpen);
        await ZipArchiveWriter.WriteAllAsync(_archive, files, ct);
    }

    public ValueTask DisposeAsync()
    {
        _archive?.Dispose();
        _archive = null;
        return ValueTask.CompletedTask;
    }
}
