namespace ValIO.Generators.TypeScript.Options;

public sealed class TypeScriptGenerationOptions
{
    public List<string> Transports { get; set; } = ["fetch"];
    public string EnumFormat { get; set; } = "union";
    public string? ValidationLibrary { get; set; }
    public string? QueryWrapper { get; set; }

    /// <summary>
    /// Mock library to emit alongside the client. Currently supports <c>msw</c> —
    /// generates a single <c>mocks/handlers.ts</c> file containing an
    /// <c>http.{verb}(...)</c> handler per IR operation. Normalised by
    /// <see cref="TypeScriptOptionsDescriptor"/> from the <c>mocks</c> /
    /// <c>mockLibrary</c> config key.
    /// </summary>
    public string? MockLibrary { get; set; }
}
