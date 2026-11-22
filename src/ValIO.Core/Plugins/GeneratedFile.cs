namespace ValIO.Core.Plugins;

/// <summary>Carries one generated file to an output sink.</summary>
public sealed class GeneratedFile : IEquatable<GeneratedFile>
{
    /// <summary>Initializes the values carried by this model.</summary>
    /// <param name="RelativePath">The normalized path relative to the output root.</param>
    /// <param name="Content">The file contents produced by the generator.</param>
    public GeneratedFile(string RelativePath, string Content)
    {
        this.RelativePath = RelativePath;
        this.Content = Content;
    }

    /// <summary>Gets the normalized path relative to the output root.</summary>
    /// <value>The normalized path relative to the output root.</value>
    public string RelativePath { get; init; }

    /// <summary>Gets the file contents produced by the generator.</summary>
    /// <value>The file contents produced by the generator.</value>
    public string Content { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(GeneratedFile? other) => other is not null
        && EqualityComparer<string>.Default.Equals(RelativePath, other.RelativePath)
        && EqualityComparer<string>.Default.Equals(Content, other.Content);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GeneratedFile other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(RelativePath, Content);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(GeneratedFile? left, GeneratedFile? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(GeneratedFile? left, GeneratedFile? right) => !Equals(left, right);
}
