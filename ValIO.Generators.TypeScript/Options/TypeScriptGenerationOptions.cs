namespace ValIO.Generators.TypeScript.Options;

public sealed class TypeScriptGenerationOptions
{
    public List<string> Transports { get; set; } = ["fetch"];
    public string EnumFormat { get; set; } = "union";
    public string? ValidationLibrary { get; set; }
    public string? QueryWrapper { get; set; }
}
