using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.CSharp.Ast.Builders;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Options;

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
        var builder = new CSharpAstBuilder(_namingConverter);

        foreach (var client in context.IrDocument.Clients)
        {
            var compilationUnit = builder.BuildCompilationUnit(client, ns);
            var content = _renderer.Render(compilationUnit);
            var clientName = string.IsNullOrWhiteSpace(client.Name) ? "ApiClient" : client.Name;
            var relativePath = $"Api/{clientName}Client.cs";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        // Generated clients share one ApiException declaration per output project.
        if (context.IrDocument.Clients.Count > 0)
        {
            var apiExceptionUnit = builder.BuildCompilationUnitApiException(ns);
            var apiExceptionContent = _renderer.Render(apiExceptionUnit);
            context.GeneratedFiles.Add(new GeneratedFile("Api/ApiException.cs", apiExceptionContent));
        }

        if (context.IrDocument.Clients
            .SelectMany(client => client.Operations)
            .Any(operation => operation.RequestBody?.IsMultipartFormData == true))
        {
            context.GeneratedFiles.Add(new GeneratedFile(
                "Api/MultipartFormDataContentFactory.cs",
                BuildMultipartFormDataContentFactory(ns)));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the source for the runtime helper that maps a typed request model to multipart content.
    /// </summary>
    /// <param name="ns">The generated project namespace.</param>
    /// <returns>The complete C# source file.</returns>
    private static string BuildMultipartFormDataContentFactory(string ns) => $$"""
        using System;
        using System.Collections;
        using System.Globalization;
        using System.IO;
        using System.Net.Http;
        using System.Net.Http.Headers;
        using System.Reflection;
        using System.Text;
        using System.Text.Json.Serialization;

        namespace {{ns}};

        /// <summary>Builds multipart HTTP content from a generated typed request model.</summary>
        internal static class MultipartFormDataContentFactory
        {
            /// <summary>Creates form-data parts using generated JSON property names as wire names.</summary>
            /// <typeparam name="TRequest">The generated request model type.</typeparam>
            /// <param name="request">The populated typed request model.</param>
            /// <returns>Multipart content owned by the generated HTTP request.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
            internal static MultipartFormDataContent Create<TRequest>(TRequest request)
            {
                ArgumentNullException.ThrowIfNull(request);
                var content = new MultipartFormDataContent();
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

                HttpContent part;
                var isFile = false;
                switch (value)
                {
                    case Stream stream:
                        part = new StreamContent(stream);
                        isFile = true;
                        break;
                    case byte[] bytes:
                        part = new ByteArrayContent(bytes);
                        isFile = true;
                        break;
                    default:
                        var text = FormatScalar(value);
                        part = new StringContent(text, Encoding.UTF8);
                        break;
                }

                if (isFile)
                {
                    part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    content.Add(part, name, name);
                }
                else
                {
                    content.Add(part, name);
                }
            }

            /// <summary>Formats non-file values with culture-independent HTTP form semantics.</summary>
            /// <param name="value">The scalar value to format.</param>
            /// <returns>The invariant wire representation.</returns>
            private static string FormatScalar(object value) => value switch
            {
                bool boolean => boolean ? "true" : "false",
                DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            };
        }
        """;
}
