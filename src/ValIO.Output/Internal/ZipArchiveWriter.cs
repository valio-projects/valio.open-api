using System.IO.Compression;
using System.Text;
using ValIO.Core.Plugins;

namespace ValIO.Output.Internal;

/// <summary>
/// Writes a sequence of <see cref="GeneratedFile"/> entries into a <see cref="ZipArchive"/>.
/// Normalizes path separators so archive entries always use '/' (cross-platform zip convention).
/// </summary>
internal static class ZipArchiveWriter
{
    public static async Task WriteAllAsync(
        ZipArchive archive,
        IReadOnlyList<GeneratedFile> files,
        CancellationToken ct = default)
    {
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var entryName = NormalizeEntryName(file.RelativePath);
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await writer.WriteAsync(file.Content.AsMemory(), ct);
        }
    }

    private static string NormalizeEntryName(string relativePath)
        => relativePath.Replace('\\', '/').TrimStart('/');
}
