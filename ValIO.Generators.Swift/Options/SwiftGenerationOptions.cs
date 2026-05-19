namespace ValIO.Generators.Swift.Options;

/// <summary>
/// Generation options for the Swift plugin.
/// </summary>
public sealed class SwiftGenerationOptions
{
    /// <summary>
    /// Module/sources directory name (single folder for generated Swift files,
    /// since Swift uses a flat compile unit per module).
    /// </summary>
    public string ModuleName { get; set; } = "Generated";

    /// <summary>
    /// Strategy for mapping IR <c>Any</c> to a Swift type.
    /// Defaults to <see cref="SwiftOptionIds.AnyMapping.AnyCodable"/>.
    /// </summary>
    public string AnyMapping { get; set; } = SwiftOptionIds.AnyMapping.AnyCodable;

    /// <summary>Whether to emit a <c>Package.swift</c> manifest. Disabled by default.</summary>
    public bool GenerateBuildFiles { get; set; } = false;
}
