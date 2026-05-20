namespace ValIO.Generators.FSharp.Options;

/// <summary>
/// Generation options for the F# plugin.
/// </summary>
public sealed class FSharpGenerationOptions
{
    /// <summary>
    /// Top-level F# namespace prefix used for emitted module declarations.
    /// </summary>
    public string Namespace { get; set; } = FSharpOptionIds.DefaultNamespace;
}
