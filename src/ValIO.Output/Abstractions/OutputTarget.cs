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
public sealed class OutputTarget : IEquatable<OutputTarget>
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

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(OutputTarget? other) => other is not null
        && EqualityComparer<OutputTargetKind>.Default.Equals(Kind, other.Kind)
        && EqualityComparer<string?>.Default.Equals(Path, other.Path)
        && EqualityComparer<Stream?>.Default.Equals(Stream, other.Stream)
        && EqualityComparer<bool>.Default.Equals(LeaveStreamOpen, other.LeaveStreamOpen);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OutputTarget other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Path, Stream, LeaveStreamOpen);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(OutputTarget? left, OutputTarget? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(OutputTarget? left, OutputTarget? right) => !Equals(left, right);
}
