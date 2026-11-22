using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.CSharp.Ast.Builders;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Options;
using ValIO.Generators.CSharp.Models;

namespace ValIO.Generators.CSharp.Steps;

/// <summary>
/// Generates typed C# HTTP clients and their shared runtime support files from the normalized IR.
/// </summary>
public sealed class CSharpClientStep : ICodeGenerationStep
{
    /// <summary>Converts wire and schema names into valid C# identifiers.</summary>
    private readonly INamingConverter _namingConverter;

    /// <summary>Renders the generated C# abstract syntax tree.</summary>
    private readonly ICSharpAstRenderer _renderer;

    /// <inheritdoc/>
    public string Name => "csharp-client";
    /// <inheritdoc/>
    public int Order => 40;

    /// <summary>Initializes the client generation step.</summary>
    /// <param name="namingConverter">The converter used for generated identifiers.</param>
    /// <param name="renderer">The renderer used for C# source output.</param>
    public CSharpClientStep(INamingConverter namingConverter, ICSharpAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    /// <inheritdoc/>
    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0;

    /// <inheritdoc/>
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as CSharpGenerationOptions;
        var ns = opts?.Namespace ?? context.Configuration.Namespace ?? "Generated";
        var builder = new CSharpAstBuilder(_namingConverter, context.DocumentContext, opts);

        if (opts?.GenerateMetrics == true && context.IrDocument.Clients.Count > 0)
        {
            context.GeneratedFiles.Add(new GeneratedFile("Api/IHttpClientMetrics.cs",
                _renderer.Render(builder.BuildCompilationUnitForIHttpClientMetrics(ns))));
            context.GeneratedFiles.Add(new GeneratedFile("Api/HttpClientMetrics.cs",
                _renderer.Render(builder.BuildCompilationUnitForHttpClientMetrics(ns))));
        }

        foreach (var client in context.IrDocument.Clients)
        {
            var compilationUnit = builder.BuildCompilationUnit(client, ns);
            var content = _renderer.Render(compilationUnit);
            var clientName = string.IsNullOrWhiteSpace(client.Name) ? "ApiClient" : client.Name;
            var relativePath = $"Api/{clientName}Client.cs";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));

            if (opts?.GenerateInterfaces == true && opts.InterfaceFileStrategy == InterfaceFileStrategy.SeparateFile)
                context.GeneratedFiles.Add(new GeneratedFile($"Api/I{clientName}.cs",
                    _renderer.Render(builder.BuildCompilationUnitForInterface(client, ns))));
            if (opts?.GenerateTraces == true || opts?.GenerateMetrics == true)
                context.GeneratedFiles.Add(new GeneratedFile($"Api/{clientName}OpenTelemetryDecorator.cs",
                    _renderer.Render(builder.BuildCompilationUnitForDecorator(client, ns))));
        }

        if (context.IrDocument.Clients.SelectMany(client => client.Operations).Any(operation =>
            operation.PathParameters.Count > 0 || operation.QueryParameters.Count > 0 ||
            operation.HeaderParameters.Count > 0 || operation.RequestBody?.IsMultipartFormData == true))
        {
            context.GeneratedFiles.Add(new GeneratedFile("Api/HttpWireValueFormatter.cs", BuildHttpWireValueFormatter(ns)));
        }

        // Generated clients share one ApiException declaration per output project.
        if (context.IrDocument.Clients.Count > 0)
        {
            var apiExceptionUnit = builder.BuildCompilationUnitApiException(ns);
            var apiExceptionContent = _renderer.Render(apiExceptionUnit);
            context.GeneratedFiles.Add(new GeneratedFile("Api/ApiException.cs", apiExceptionContent));
        }

        if (HasMultipartOperations(context))
        {
            AddMultipartFileType(context, ns);
            context.GeneratedFiles.Add(new GeneratedFile(
                "Api/MultipartFormDataContentFactory.cs",
                BuildMultipartFormDataContentFactory(ns)));
        }

        return Task.CompletedTask;
    }

    /// <summary>Finds whether any operation requires multipart request support.</summary>
    /// <param name="context">The document and configuration for one generated project.</param>
    /// <returns>Whether the document includes a form-data operation.</returns>
    internal static bool HasMultipartOperations(GenerationContext context) => context.IrDocument.Clients
        .SelectMany(client => client.Operations)
        .Any(operation => operation.RequestBody?.IsMultipartFormData == true);

    /// <summary>Emits the borrowed file descriptor for model-only and client generation.</summary>
    /// <param name="context">The output collection and its schema declarations.</param>
    /// <param name="ns">The namespace of the request models.</param>
    /// <exception cref="InvalidOperationException">A schema declaration already uses the generated descriptor name.</exception>
    internal static void AddMultipartFileType(GenerationContext context, string ns)
    {
        const string path = "Models/MultipartFile.cs";
        if (context.IrDocument.Models.Any(model => model.Name == "MultipartFile") ||
            context.IrDocument.Enums.Any(model => model.Name == "MultipartFile"))
            throw new InvalidOperationException("Schema name 'MultipartFile' conflicts with the C# multipart file descriptor.");

        if (context.GeneratedFiles.Any(file => file.RelativePath == path))
            return;

        context.GeneratedFiles.Add(new GeneratedFile(path, $$"""
            using System;
            using System.IO;

            namespace {{ns}};

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
            """));
    }

    /// <summary>
    /// Builds the source for the runtime helper that maps a typed request model to multipart content.
    /// </summary>
    /// <param name="ns">The generated project namespace.</param>
    /// <returns>The complete C# source file.</returns>
    private static string BuildMultipartFormDataContentFactory(string ns) => $$"""
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

        namespace {{ns}};

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
            /// <param name="source">The readable stream owned by the request caller.</param>
            private sealed class BorrowedReadStream(Stream source) : Stream
            {
                /// <inheritdoc />
                public override bool CanRead => source.CanRead;

                /// <inheritdoc />
                public override bool CanSeek => source.CanSeek;

                /// <inheritdoc />
                public override bool CanWrite => false;

                /// <inheritdoc />
                public override long Length => source.Length;

                /// <inheritdoc />
                public override long Position { get => source.Position; set => source.Position = value; }

                /// <inheritdoc />
                public override void Flush() { }

                /// <inheritdoc />
                public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);

                /// <inheritdoc />
                public override int Read(Span<byte> buffer) => source.Read(buffer);

                /// <inheritdoc />
                public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                    source.ReadAsync(buffer, offset, count, cancellationToken);

                /// <inheritdoc />
                public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                    source.ReadAsync(buffer, cancellationToken);

                /// <inheritdoc />
                public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
                    source.CopyToAsync(destination, bufferSize, cancellationToken);

                /// <inheritdoc />
                public override long Seek(long offset, SeekOrigin origin) => source.Seek(offset, origin);

                /// <inheritdoc />
                public override void SetLength(long value) => throw new NotSupportedException();

                /// <inheritdoc />
                public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            }

        }
        """;

    /// <summary>Builds the shared formatter used by URL, header, and multipart scalar values.</summary>
    /// <param name="ns">The generated project namespace.</param>
    /// <returns>A standalone runtime helper honoring enum wire names and invariant formats.</returns>
    private static string BuildHttpWireValueFormatter(string ns) => $$"""
        using System;
        using System.Collections.Generic;
        using System.Globalization;
        using System.Linq;
        using System.Reflection;
        using System.Text.Json.Serialization;

        namespace {{ns}};

        /// <summary>Formats scalar HTTP values consistently across URL, header, and form transports.</summary>
        internal static class HttpWireValueFormatter
        {
            /// <summary>Formats one scalar using its invariant wire representation.</summary>
            /// <param name="value">The scalar value, or null.</param>
            /// <returns>The invariant text before transport-specific escaping.</returns>
            /// <exception cref="NotSupportedException">The value is not a supported HTTP scalar.</exception>
            /// <exception cref="ArgumentOutOfRangeException">An enum value is outside its declared members.</exception>
            internal static string Format(object? value) => value switch
            {
                null => string.Empty,
                string text => text,
                bool boolean => boolean ? "true" : "false",
                DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeOnly time => time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
                DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
                Enum enumValue => FormatEnum(enumValue),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => throw new NotSupportedException($"The HTTP scalar type {value.GetType().Name} is not supported."),
            };

            /// <summary>Formats an HTTP collection with separators outside the optional per-item escaping.</summary>
            /// <typeparam name="T">The generated scalar item type.</typeparam>
            /// <param name="values">The collection to format.</param>
            /// <param name="separator">The delimiter between adjacent items.</param>
            /// <param name="escape">Whether to percent-encode the individual items.</param>
            /// <returns>The formatted values, separated by the declared delimiter.</returns>
            internal static string JoinValues<T>(IEnumerable<T> values, string separator, bool escape) =>
                string.Join(separator, values.Select(value =>
                {
                    var text = Format(value);
                    return escape ? Uri.EscapeDataString(text) : text;
                }));

            /// <summary>Uses declared string wire names and underlying numeric values for enum fields.</summary>
            /// <param name="value">The declared enum member.</param>
            /// <returns>The original schema value.</returns>
            /// <exception cref="ArgumentOutOfRangeException">The enum value has no declared member.</exception>
            private static string FormatEnum(Enum value)
            {
                var type = value.GetType();
                var memberName = Enum.GetName(type, value)
                    ?? throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is not declared by the schema.");
                var wireName = type.GetField(memberName)?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name;
                return wireName ?? Convert.ToString(
                    Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
        """;
}
