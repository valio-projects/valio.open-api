namespace ValIO.Output.Abstractions;

/// <summary>
/// Where generated files should be materialized.
/// </summary>
public enum OutputTargetKind
{
    /// <summary>Write files to a directory tree on disk.</summary>
    Disk,

    /// <summary>Write files into a .zip archive at the given <see cref="OutputTarget.Path"/>.</summary>
    ZipFile,

    /// <summary>Write files into a .zip archive backed by the provided <see cref="OutputTarget.Stream"/>.</summary>
    ZipStream,

    /// <summary>Keep files in memory; consumer reads them back as a dictionary.</summary>
    InMemory
}

/// <summary>
/// Immutable description of an output destination. Pick one of the supported <see cref="OutputTargetKind"/> values
/// and fill in only the corresponding fields.
/// </summary>
public sealed record OutputTarget
{
    public required OutputTargetKind Kind { get; init; }

    /// <summary>Filesystem path. Required for <see cref="OutputTargetKind.Disk"/> and <see cref="OutputTargetKind.ZipFile"/>.</summary>
    public string? Path { get; init; }

    /// <summary>Writable stream. Required for <see cref="OutputTargetKind.ZipStream"/>.</summary>
    public Stream? Stream { get; init; }

    /// <summary>When <see cref="Kind"/> is <see cref="OutputTargetKind.ZipStream"/>, leave the underlying stream open after the sink is disposed.</summary>
    public bool LeaveStreamOpen { get; init; }

    public static OutputTarget Folder(string path) => new() { Kind = OutputTargetKind.Disk, Path = path };
    public static OutputTarget ZipPath(string path) => new() { Kind = OutputTargetKind.ZipFile, Path = path };
    public static OutputTarget ToStream(Stream stream, bool leaveOpen = true)
        => new() { Kind = OutputTargetKind.ZipStream, Stream = stream, LeaveStreamOpen = leaveOpen };
    public static OutputTarget Memory() => new() { Kind = OutputTargetKind.InMemory };
}
