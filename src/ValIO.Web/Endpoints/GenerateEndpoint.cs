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
    public static async Task<IResult> HandleAsync(
        GenerateRequest req,
        IGenerationOrchestrator orchestrator,
        IGeneratorRegistry registry,
        IOutputSinkFactory sinkFactory,
        ISafeSchemaFetcher fetcher,
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

        var fileName = $"valio-{req.LanguageId}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
        return Results.Stream(ms, "application/zip", fileName);
    }
}
