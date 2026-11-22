namespace ValIO.Desktop.Services;

/// <summary>
/// Lightweight summary of an OpenAPI document, computed once after schema selection.
/// Used by the GenerationView preview pane (and useful in error toasts).
/// </summary>
public sealed class SchemaPreview : IEquatable<SchemaPreview>
{
    /// <summary>Initializes the values carried by this model.</summary>
    /// <param name="Title">The title displayed in the desktop preview.</param>
    /// <param name="Version">The version declared by the document.</param>
    /// <param name="Paths">The number of OpenAPI paths.</param>
    /// <param name="Operations">The number of HTTP operations.</param>
    /// <param name="Components">The number of reusable component schemas.</param>
    /// <param name="FileSizeBytes">The source file size in bytes, or zero for a remote document.</param>
    public SchemaPreview(
        string Title,
        string Version,
        int Paths,
        int Operations,
        int Components,
        long FileSizeBytes)
    {
        this.Title = Title;
        this.Version = Version;
        this.Paths = Paths;
        this.Operations = Operations;
        this.Components = Components;
        this.FileSizeBytes = FileSizeBytes;
    }

    /// <summary>Gets the title displayed in the desktop preview.</summary>
    /// <value>The title displayed in the desktop preview.</value>
    public string Title { get; init; }

    /// <summary>Gets the version declared by the document.</summary>
    /// <value>The version declared by the document.</value>
    public string Version { get; init; }

    /// <summary>Gets the number of OpenAPI paths.</summary>
    /// <value>The number of OpenAPI paths.</value>
    public int Paths { get; init; }

    /// <summary>Gets the number of HTTP operations.</summary>
    /// <value>The number of HTTP operations.</value>
    public int Operations { get; init; }

    /// <summary>Gets the number of reusable component schemas.</summary>
    /// <value>The number of reusable component schemas.</value>
    public int Components { get; init; }

    /// <summary>Gets the source file size in bytes, or zero for a remote document.</summary>
    /// <value>The source file size in bytes, or zero for a remote document.</value>
    public long FileSizeBytes { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(SchemaPreview? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Title, other.Title)
        && EqualityComparer<string>.Default.Equals(Version, other.Version)
        && EqualityComparer<int>.Default.Equals(Paths, other.Paths)
        && EqualityComparer<int>.Default.Equals(Operations, other.Operations)
        && EqualityComparer<int>.Default.Equals(Components, other.Components)
        && EqualityComparer<long>.Default.Equals(FileSizeBytes, other.FileSizeBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SchemaPreview other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Title, Version, Paths, Operations, Components, FileSizeBytes);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(SchemaPreview? left, SchemaPreview? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(SchemaPreview? left, SchemaPreview? right) => !Equals(left, right);

    /// <summary>Formats the source size for the desktop preview.</summary>
    /// <value>The size formatted with an appropriate binary unit.</value>
    public string FileSizeDisplay => FileSizeBytes switch
    {
        < 1024              => $"{FileSizeBytes} B",
        < 1024 * 1024       => $"{FileSizeBytes / 1024.0:F1} KB",
        _                   => $"{FileSizeBytes / 1024.0 / 1024.0:F1} MB",
    };
}
