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
/// <param name="namingConverter">The converter used for generated identifiers.</param>
/// <param name="renderer">The renderer used for C# source output.</param>
public sealed class CSharpClientStep(INamingConverter namingConverter, ICSharpAstRenderer renderer) : ICodeGenerationStep
{


    /// <inheritdoc/>
    public string Name => "csharp-client";
    /// <inheritdoc/>
    public int Order => 40;

    /// <inheritdoc/>
    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0;

    /// <inheritdoc/>
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as CSharpGenerationOptions;
        var ns = opts?.Namespace ?? context.Configuration.Namespace ?? "Generated";
        var builder = new CSharpAstBuilder(namingConverter, context.DocumentContext, opts);

        if (opts?.GenerateMetrics == true && context.IrDocument.Clients.Count > 0)
        {
            context.GeneratedFiles.Add(new GeneratedFile("Api/IHttpClientMetrics.cs",
                renderer.Render(builder.BuildCompilationUnitForIHttpClientMetrics(ns))));
            context.GeneratedFiles.Add(new GeneratedFile("Api/HttpClientMetrics.cs",
                renderer.Render(builder.BuildCompilationUnitForHttpClientMetrics(ns))));
        }

        if (opts?.GenerateResponseMetadata == true)
        {
            var names = context.IrDocument.Models.Select(model => model.Name)
                .Concat(context.IrDocument.Enums.Select(model => model.Name))
                .Concat(context.IrDocument.Clients.Select(client => client.Name));
            if (names.Any(name => name is "ApiResponseContract" or "ApiResponseReader"))
                throw new InvalidOperationException("A C# schema or client name conflicts with the generated response metadata support types.");

            if (opts.ResponseMetadataOperations is { } selection)
            {
                var ids = context.IrDocument.Clients.SelectMany(client => client.Operations)
                    .Select(operation => operation.OriginalMethodName).ToHashSet(StringComparer.Ordinal);
                var unknown = selection.Keys.Where(name => !ids.Contains(name)).ToArray();
                if (unknown.Length > 0)
                    throw new InvalidOperationException($"Unknown C# response metadata operations: {string.Join(", ", unknown)}.");
            }

            context.GeneratedFiles.Add(new GeneratedFile("Api/ApiResponse.cs", CSharpRuntimeSource.Render("ApiResponse.cs", ns)));
        }

        foreach (var client in context.IrDocument.Clients)
        {
            var compilationUnit = builder.BuildCompilationUnit(client, ns);
            var content = renderer.Render(compilationUnit);
            var clientName = string.IsNullOrWhiteSpace(client.Name) ? "ApiClient" : client.Name;
            var relativePath = $"Api/{clientName}Client.cs";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));

            if (opts?.GenerateInterfaces == true && opts.InterfaceFileStrategy == InterfaceFileStrategy.SeparateFile)
                context.GeneratedFiles.Add(new GeneratedFile($"Api/I{clientName}.cs",
                    renderer.Render(builder.BuildCompilationUnitForInterface(client, ns))));
            if (opts?.GenerateTraces == true || opts?.GenerateMetrics == true)
                context.GeneratedFiles.Add(new GeneratedFile($"Api/{clientName}OpenTelemetryDecorator.cs",
                    renderer.Render(builder.BuildCompilationUnitForDecorator(client, ns))));
        }

        if (context.IrDocument.Clients.SelectMany(client => client.Operations).Any(operation =>
            operation.PathParameters.Count > 0 || operation.QueryParameters.Count > 0 ||
            operation.HeaderParameters.Count > 0 || operation.RequestBody?.IsMultipartFormData == true))
        {
            context.GeneratedFiles.Add(new GeneratedFile("Api/HttpWireValueFormatter.cs", CSharpRuntimeSource.Render("HttpWireValueFormatter.cs", ns)));
        }

        // Generated clients share one ApiException declaration per output project.
        if (context.IrDocument.Clients.Count > 0)
        {
            var apiExceptionUnit = builder.BuildCompilationUnitApiException(ns);
            var apiExceptionContent = renderer.Render(apiExceptionUnit);
            context.GeneratedFiles.Add(new GeneratedFile("Api/ApiException.cs", apiExceptionContent));
        }

        if (HasMultipartOperations(context))
        {
            AddMultipartFileType(context, ns);
            context.GeneratedFiles.Add(new GeneratedFile(
                "Api/MultipartFormDataContentFactory.cs",
                CSharpRuntimeSource.Render("MultipartFormDataContentFactory.cs", ns)));
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

        context.GeneratedFiles.Add(new GeneratedFile(path, CSharpRuntimeSource.Render("MultipartFile.cs", ns)));
    }
}
