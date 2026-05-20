using System.Text.Json;

namespace ValIO.Web.Endpoints;

/// <summary>JSON payload received by POST /api/generate.</summary>
public sealed class GenerateRequest
{
    public string? SchemaUrl { get; set; }
    public string? SchemaFileName { get; set; }
    public string? SchemaContentBase64 { get; set; }
    public string LanguageId { get; set; } = "";
    public string? Namespace { get; set; }
    public bool GenerateModels { get; set; } = true;
    public bool GenerateClients { get; set; } = true;
    public bool GenerateEnums { get; set; } = true;

    /// <summary>
    /// Raw JSON object passed straight through to <c>IGeneratorOptionsDescriptor.ParseOptions</c>.
    /// We keep it as <see cref="JsonElement"/> instead of <c>Dictionary&lt;string, object?&gt;</c>
    /// so the plugin sees actual JSON types (array, bool, string) — not boxed JsonElements.
    /// </summary>
    public JsonElement? PluginOptions { get; set; }
}
