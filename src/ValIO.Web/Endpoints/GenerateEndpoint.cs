using System.Globalization;
using System.Text.Json;
using ValIO.Application.Services;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Output.Abstractions;
using ValIO.Web.Services;

namespace ValIO.Web.Endpoints;

/// <summary>
/// POST /api/generate — runs orchestrator, streams a zip back to the client.
/// </summary>
public static class GenerateEndpoint
{
    /// <summary>Generates the requested SDK and returns its files as a ZIP archive.</summary>
    /// <param name="req">The schema reference and generator configuration.</param>
    /// <param name="orchestrator">The configured generation pipeline coordinator.</param>
    /// <param name="registry">The catalog of available language generators.</param>
    /// <param name="sinkFactory">The factory that writes the generated archive.</param>
    /// <param name="fetcher">The service preparing an authorized schema input.</param>
    /// <param name="clock">The host clock used for the archive name.</param>
    /// <param name="ct">The cancellation token for the request.</param>
    /// <returns>The generated archive or the applicable HTTP problem.</returns>
    public static async Task<IResult> HandleAsync(
        GenerateRequest req,
        IGenerationOrchestrator orchestrator,
        IGeneratorRegistry registry,
        IOutputSinkFactory sinkFactory,
        ISafeSchemaFetcher fetcher,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.LanguageId))
            return Results.BadRequest("languageId is required");

        var plugin = registry.FindPlugin(req.LanguageId);
        if (plugin is null)
            return Results.BadRequest($"Unknown language: {req.LanguageId}");

        // Plugin options — pass through OptionsDescriptor.ParseOptions to get a typed
        // POCO. Skipping this step is what caused multi-transport/validation choices
        // submitted by the form to be silently ignored.
        object? pluginOptions = null;
        if (req.PluginOptions is { ValueKind: JsonValueKind.Object } el)
        {
            var raw = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var prop in el.EnumerateObject())
                raw[prop.Name] = prop.Value;
            pluginOptions = plugin.OptionsDescriptor.ParseOptions(raw, out var optErrors);
            if (optErrors.Count > 0)
                return Results.BadRequest("Invalid options: " + string.Join("; ", optErrors));
        }

        string schemaPath;
        try
        {
            if (!string.IsNullOrWhiteSpace(req.SchemaUrl))
            {
                schemaPath = await fetcher.PrepareUrlAsync(req.SchemaUrl, ct);
            }
            else if (!string.IsNullOrWhiteSpace(req.SchemaContentBase64))
            {
                var bytes = Convert.FromBase64String(req.SchemaContentBase64);
                schemaPath = await fetcher.PrepareUploadAsync(req.SchemaFileName ?? "schema.json", bytes, ct);
            }
            else
            {
                return Results.BadRequest("Either schemaUrl or schemaContentBase64 is required");
            }
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        var config = new GenerationConfiguration
        {
            LanguageId = req.LanguageId,
            OutputPath = "/",
            Namespace = req.Namespace,
            GenerateModels = req.GenerateModels,
            GenerateClients = req.GenerateClients,
            GenerateEnums = req.GenerateEnums,
            PluginOptions = pluginOptions,
        };

        var result = await orchestrator.GenerateAsync(config, schemaPath, ct);
        if (!result.Success)
            return Results.Problem(result.ErrorMessage ?? "Generation failed");

        var ms = new MemoryStream();
        await using (var sink = sinkFactory.Create(OutputTarget.ToStream(ms, leaveOpen: true)))
        {
            await sink.WriteAsync(result.Files, ct);
        }
        ms.Position = 0;

        var timestamp = clock.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var fileName = $"valio-{req.LanguageId}-{timestamp}.zip";
        return Results.Stream(ms, "application/zip", fileName);
    }
}
