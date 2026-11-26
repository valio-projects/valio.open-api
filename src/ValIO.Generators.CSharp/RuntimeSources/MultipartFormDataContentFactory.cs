using System;
using System.Collections;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Builds multipart HTTP content from a generated typed request model.</summary>
internal static class MultipartFormDataContentFactory
{
    /// <summary>Creates form-data parts using generated JSON property names as wire names.</summary>
    /// <typeparam name="TRequest">The generated request model type.</typeparam>
    /// <param name="request">The populated typed request model.</param>
    /// <returns>Multipart content owned by the generated HTTP request, borrowing any file streams.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    internal static MultipartFormDataContent Create<TRequest>(TRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = new MultipartFormDataContent();
        try
        {
            foreach (var property in typeof(TRequest).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var value = property.GetValue(request);
                if (value is null)
                {
                    continue;
                }

                var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
                AddValue(content, name, value);
            }

            return content;
        }
        catch
        {
            content.Dispose();
            throw;
        }
    }

    /// <summary>Adds one scalar, file, or repeated value to the multipart payload.</summary>
    /// <param name="content">The multipart payload being assembled.</param>
    /// <param name="name">The wire-level form field name.</param>
    /// <param name="value">The non-null field value.</param>
    private static void AddValue(MultipartFormDataContent content, string name, object value)
    {
        if (value is IEnumerable values and not string and not byte[] and not Stream)
        {
            foreach (var item in values)
            {
                if (item is not null)
                {
                    AddValue(content, name, item);
                }
            }

            return;
        }

        switch (value)
        {
            case MultipartFile file:
                AddFile(content, name, new StreamContent(new BorrowedReadStream(file.Content)), file.FileName, file.ContentType);
                break;
            case Stream stream:
                AddFile(content, name, new StreamContent(new BorrowedReadStream(stream)), name, "application/octet-stream");
                break;
            case byte[] bytes:
                AddFile(content, name, new ByteArrayContent(bytes), name, "application/octet-stream");
                break;
            default:
                content.Add(new StringContent(HttpWireValueFormatter.Format(value), Encoding.UTF8), name);
                break;
        }
    }

    /// <summary>Adds a named file part and releases its HTTP content if adding it fails.</summary>
    /// <param name="content">The multipart message that takes ownership on success.</param>
    /// <param name="name">The exact wire field name, shared by repeated parts.</param>
    /// <param name="part">The HTTP content for one file.</param>
    /// <param name="fileName">The transmitted file name.</param>
    /// <param name="contentType">The media type for this file part.</param>
    /// <exception cref="FormatException">The supplied media type is invalid.</exception>
    private static void AddFile(MultipartFormDataContent content, string name, HttpContent part, string fileName, string contentType)
    {
        try
        {
            part.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            content.Add(part, name, fileName);
        }
        catch
        {
            part.Dispose();
            throw;
        }
    }

    /// <summary>Lets StreamContent dispose its wrapper without taking ownership of the source stream.</summary>
    private sealed class BorrowedReadStream : Stream
    {
        /// <summary>The underlying stream owned by the request caller.</summary>
        private readonly Stream _source;

        /// <summary>Wraps a stream for serialization without closing it with the content.</summary>
        /// <param name="source">The readable stream owned by the request caller.</param>
        public BorrowedReadStream(Stream source)
        {
            _source = source;
        }

        /// <inheritdoc />
        public override bool CanRead => _source.CanRead;

        /// <inheritdoc />
        public override bool CanSeek => _source.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => _source.Length;

        /// <inheritdoc />
        public override long Position { get => _source.Position; set => _source.Position = value; }

        /// <inheritdoc />
        public override void Flush() { }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => _source.Read(buffer, offset, count);

        /// <inheritdoc />
        public override int Read(Span<byte> buffer) => _source.Read(buffer);

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _source.ReadAsync(buffer, offset, count, cancellationToken);

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _source.ReadAsync(buffer, cancellationToken);

        /// <inheritdoc />
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
            _source.CopyToAsync(destination, bufferSize, cancellationToken);

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => _source.Seek(offset, origin);

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

}
