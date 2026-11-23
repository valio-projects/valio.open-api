namespace ValIO.Generators.CSharp.Steps;

/// <summary>Renders the shared response snapshot and bounded reader for opt-in generated C# clients.</summary>
internal static class CSharpResponseMetadataSource
{
    /// <summary>Builds the response runtime in the same namespace as the generated client.</summary>
    /// <param name="ns">The output namespace.</param>
    /// <returns>A complete C# source file for the response-aware profile.</returns>
    internal static string Build(string ns)
    {
        var namespaceLine = string.IsNullOrWhiteSpace(ns) ? string.Empty : $"namespace {ns};";
        return $$"""
            #nullable enable
            using System;
            using System.Buffers;
            using System.Collections.Generic;
            using System.Collections.ObjectModel;
            using System.Diagnostics.CodeAnalysis;
            using System.Linq;
            using System.Net.Http;
            using System.Text.Json;
            using System.Threading;
            using System.Threading.Tasks;

            {{namespaceLine}}

            /// <summary>Captures the actual HTTP response and any payload verified against its declared wire contract.</summary>
            /// <typeparam name="T">The primary body type declared for a successful response.</typeparam>
            public sealed class ApiResponse<T>
            {
                /// <summary>The decoded non-null payload for the observed status and media type.</summary>
                private readonly object? _body;

                /// <summary>Snapshots response metadata before the underlying response is disposed.</summary>
                /// <param name="response">The observed HTTP response.</param>
                /// <param name="body">The validated body or null when no body was decoded.</param>
                /// <param name="isBodyless">Whether the response confirms a declared bodyless outcome.</param>
                internal ApiResponse(HttpResponseMessage response, object? body = null, bool isBodyless = false)
                {
                    _body = body;
                    StatusCode = (int)response.StatusCode;
                    ContentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
                    CacheControlNoStore = response.Headers.CacheControl?.NoStore == true;
                    IsBodyless = isBodyless;
                    var headers = response.Headers.Concat(response.Content.Headers)
                        .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key.ToLowerInvariant(),
                            group => (IReadOnlyList<string>)Array.AsReadOnly(group.SelectMany(header => header.Value).ToArray()),
                            StringComparer.OrdinalIgnoreCase);
                    Headers = new ReadOnlyDictionary<string, IReadOnlyList<string>>(headers);
                }

                /// <summary>Gets the observed numeric status code, including non-success responses.</summary>
                /// <value>The actual HTTP status code.</value>
                public int StatusCode { get; }

                /// <summary>Gets the normalized media type of the observed response.</summary>
                /// <value>The lowercase type and subtype without parameters, or null if unavailable.</value>
                public string? ContentType { get; }

                /// <summary>Gets a case-insensitive snapshot of received response and content headers.</summary>
                /// <value>The observed names and their values, with no synthetic entries for missing headers.</value>
                public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

                /// <summary>Gets whether the received Cache-Control header specifies no-store.</summary>
                /// <value>True when the HTTP header carries the parsed no-store directive.</value>
                public bool CacheControlNoStore { get; }

                /// <summary>Gets whether a declared empty response matches the actual transport.</summary>
                /// <value>False for an unknown, malformed, or unexpectedly empty typed response.</value>
                public bool IsBodyless { get; }

                /// <summary>Gets whether the response carried a confirmed primary body.</summary>
                /// <value>True when <see cref="Body"/> holds a decoded primary payload.</value>
                public bool HasBody => _body is T;

                /// <summary>Gets the decoded primary payload, also available for an error using the same schema.</summary>
                /// <value>The payload or the default value of <typeparamref name="T"/> when <see cref="HasBody"/> is false.</value>
                public T? Body => _body is T value ? value : default;

                /// <summary>Tries to obtain a body decoded from the declared status and media type.</summary>
                /// <typeparam name="TBody">The requested primary or alternate result model.</typeparam>
                /// <param name="body">The typed payload, when the result is true.</param>
                /// <returns>True when the result contains the requested payload type.</returns>
                public bool TryGetBody<TBody>([NotNullWhen(true)] out TBody? body)
                {
                    if (_body is TBody value)
                    {
                        body = value;
                        return true;
                    }

                    body = default;
                    return false;
                }
            }

            /// <summary>Describes a status and one response representation emitted from OpenAPI.</summary>
            internal sealed class ApiResponseContract
            {
                /// <summary>Initializes one response status and media alternative.</summary>
                /// <param name="status">An exact code or a negative status family.</param>
                /// <param name="mediaType">The declared media type, or null for no body.</param>
                /// <param name="bodyType">The CLR type that can be decoded, or null when no type is available.</param>
                internal ApiResponseContract(int status, string? mediaType, Type? bodyType)
                {
                    Status = status;
                    MediaType = mediaType;
                    BodyType = bodyType;
                }

                /// <summary>Gets the response status selector.</summary>
                /// <value>The numeric status or the negated leading digit of a status range.</value>
                internal int Status { get; }

                /// <summary>Gets the declared media type.</summary>
                /// <value>The type and subtype, or null for a bodyless response.</value>
                internal string? MediaType { get; }

                /// <summary>Gets the CLR payload type associated with the representation.</summary>
                /// <value>The known JSON body type, or null for no schema or unsupported content.</value>
                internal Type? BodyType { get; }
            }

            /// <summary>Sends the request once and reads only the body selected by its observed status and media type.</summary>
            internal static class ApiResponseReader
            {
                /// <summary>Applies one HTTP timeout to headers and to bounded body reading.</summary>
                /// <typeparam name="T">The primary operation result type.</typeparam>
                /// <param name="http">The caller-configured HTTP client.</param>
                /// <param name="request">The request owned by the generated operation.</param>
                /// <param name="maxBodyBytes">The maximum decodable body size.</param>
                /// <param name="cancellationToken">The original request cancellation token.</param>
                /// <param name="contracts">The operation's exact and range response declarations.</param>
                /// <returns>The observed response metadata and any confirmed body.</returns>
                /// <exception cref="HttpRequestException">The transport failed or its timeout expired.</exception>
                /// <exception cref="OperationCanceledException">The caller canceled the request.</exception>
                internal static async Task<ApiResponse<T>> SendAsync<T>(HttpClient http, HttpRequestMessage request, int maxBodyBytes,
                    CancellationToken cancellationToken, params ApiResponseContract[] contracts)
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    deadline.CancelAfter(http.Timeout);
                    try
                    {
                        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                        var status = (int)response.StatusCode;
                        var selector = contracts.Any(contract => contract.Status == status) ? status : -(status / 100);
                        var mediaType = response.Content.Headers.ContentType?.MediaType;
                        foreach (var contract in contracts)
                        {
                            if (contract.Status != selector)
                                continue;

                            if (contract.MediaType is null)
                            {
                                var empty = request.Method == HttpMethod.Head || status is 204 or 205 or 304 ||
                                    await HasEmptyBodyAsync(response.Content, deadline.Token).ConfigureAwait(false);
                                return new ApiResponse<T>(response, isBodyless: empty);
                            }

                            if (contract.BodyType is { } bodyType && string.Equals(contract.MediaType, mediaType, StringComparison.OrdinalIgnoreCase))
                            {
                                var body = await ReadJsonAsync(response.Content, bodyType, maxBodyBytes, deadline.Token).ConfigureAwait(false);
                                return new ApiResponse<T>(response, body);
                            }
                        }

                        return new ApiResponse<T>(response);
                    }
                    catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new HttpRequestException("The HTTP response deadline expired.", error);
                    }
                }

                /// <summary>Checks an explicitly bodyless response without buffering an unexpected payload.</summary>
                /// <param name="content">The received HTTP content.</param>
                /// <param name="cancellationToken">The combined request deadline.</param>
                /// <returns>True when the content stream ends before yielding a byte.</returns>
                private static async Task<bool> HasEmptyBodyAsync(HttpContent content, CancellationToken cancellationToken)
                {
                    if (content.Headers.ContentLength is > 0)
                        return false;

                    var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    return await stream.ReadAsync(new byte[1].AsMemory(), cancellationToken).ConfigureAwait(false) == 0;
                }

                /// <summary>Reads one bounded UTF-8 JSON payload and checks the declared CLR model.</summary>
                /// <param name="content">The HTTP content when its declared media type matched.</param>
                /// <param name="bodyType">The CLR type declared for the observed response.</param>
                /// <param name="maxBodyBytes">The maximum accepted size in bytes.</param>
                /// <param name="cancellationToken">The combined request deadline.</param>
                /// <returns>The typed JSON body, or null for missing, malformed, or oversized content.</returns>
                private static async Task<object?> ReadJsonAsync(HttpContent content, Type bodyType, int maxBodyBytes,
                    CancellationToken cancellationToken)
                {
                    if (content.Headers.ContentLength > maxBodyBytes)
                        return null;

                    var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    var buffer = ArrayPool<byte>.Shared.Rent(maxBodyBytes + 1);
                    try
                    {
                        var count = await stream.ReadAtLeastAsync(buffer.AsMemory(0, maxBodyBytes + 1), maxBodyBytes + 1,
                            throwOnEndOfStream: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (count == 0 || count > maxBodyBytes)
                            return null;

                        return JsonSerializer.Deserialize(buffer.AsSpan(0, count), bodyType, JsonSerializerOptions.Default);
                    }
                    catch (JsonException)
                    {
                        return null;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                    }
                }
            }
            """;
    }
}
