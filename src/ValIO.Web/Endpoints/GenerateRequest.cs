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
    public Dictionary<string, object?>? PluginOptions { get; set; }
}
