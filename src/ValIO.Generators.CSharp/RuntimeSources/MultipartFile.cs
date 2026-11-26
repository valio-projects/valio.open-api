using System;
using System.IO;

/// <summary>Describes one file part with a caller-owned readable stream.</summary>
/// <remarks>The request reads from the current position. Keep the stream open until the
/// awaited call completes. The caller disposes the stream and controls its position.</remarks>
public sealed class MultipartFile
{
    /// <summary>Creates a file part with its transmitted name and media type.</summary>
    /// <param name="content">The caller-owned stream positioned at the first byte to send.</param>
    /// <param name="fileName">The file name written to Content-Disposition.</param>
    /// <param name="contentType">The media type written to this part's Content-Type.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The stream is unreadable or a metadata value is blank.</exception>
    public MultipartFile(Stream content, string fileName, string contentType = "application/octet-stream")
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (!content.CanRead)
            throw new ArgumentException("The file stream must be readable.", nameof(content));

        Content = content;
        FileName = fileName;
        ContentType = contentType;
    }

    /// <summary>Gets the source of the part's bytes.</summary>
    /// <value>A readable stream that remains owned by the caller.</value>
    public Stream Content { get; }

    /// <summary>Gets the transmitted file name.</summary>
    /// <value>The name used in the part's Content-Disposition header.</value>
    public string FileName { get; }

    /// <summary>Gets the transmitted file media type.</summary>
    /// <value>The media type used in the part's Content-Type header.</value>
    public string ContentType { get; }
}
